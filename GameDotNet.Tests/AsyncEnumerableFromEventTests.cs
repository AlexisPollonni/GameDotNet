using GameDotNet.Core.Tooling.Extensions;

namespace GameDotNet.Tests;

public class AsyncEnumerableFromEventTests
{
    [Test]
    public async Task FromEvent_ShouldYieldEvents_WhenEventsAreRaised(CancellationToken token)
    {
        // Arrange
        var eventSource = new TestEventSource();
        var receivedEvents = new List<int>();

        var events = AsyncEnumerable.FromEvent<int>(
            h => eventSource.NumberEvent += h,
            h => eventSource.NumberEvent -= h, cancellationToken: token);

        // Act
        var enumerationTask = Task.Run(async () =>
        {
            await foreach (var evt in events)
            {
                receivedEvents.Add(evt);
                if (receivedEvents.Count >= 3)
                    break;
            }
        }, token);

        // Give time for enumeration to start
        await Task.Delay(50, token);

        eventSource.RaiseNumber(1);
        eventSource.RaiseNumber(2);
        eventSource.RaiseNumber(3);

        await enumerationTask;

        // Assert
        await Assert.That(receivedEvents).IsEquivalentTo([1, 2, 3]);
    }

    [Test]
    public async Task FromEvent_ShouldUnsubscribe_WhenEnumerationCompletes(CancellationToken token)
    {
        // Arrange
        var eventSource = new TestEventSource();

        var events = AsyncEnumerable.FromEvent<int>(
            h => eventSource.NumberEvent += h,
            h => eventSource.NumberEvent -= h, cancellationToken: token);

        // Act
        var enumerationTask = Task.Run(async () =>
        {
            await foreach (var evt in events)
            {
                if (evt == 1)
                    break;
            }
        }, token);

        await Task.Delay(50, token);
        eventSource.RaiseNumber(1);
        await enumerationTask;

        // Assert - no subscribers should remain
        await Assert.That(eventSource.HasNumberSubscribers).IsFalse();
    }

    [Test]
    public async Task FromEvent_ShouldStopEnumeration_WhenCancelled(CancellationToken token)
    {
        // Arrange
        var eventSource = new TestEventSource();
        var receivedEvents = new List<int>();
        var cts = new CancellationTokenSource();
        var testToken = cts.Token;

        var events = AsyncEnumerable.FromEvent<int>(
            h => eventSource.NumberEvent += h,
            h => eventSource.NumberEvent -= h, cancellationToken: testToken);

        // Act
        var enumerationTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var evt in events)
                {
                    receivedEvents.Add(evt);
                    await Task.Delay(100, testToken); // Simulate slow processing
                }
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }, token);

        await Task.Delay(50, token);
        eventSource.RaiseNumber(1);
        await Task.Delay(50, token);
        await cts.CancelAsync();

        await enumerationTask;
        cts.Dispose();

        // Assert
        await Assert.That(receivedEvents.Count).IsLessThanOrEqualTo(1);
        await Assert.That(eventSource.HasNumberSubscribers).IsFalse();
    }

    [Test]
    public async Task FromEvent_WithEventHandler_ShouldYieldEventArgs(CancellationToken token)
    {
        // Arrange
        var eventSource = new TestEventSource();
        var receivedArgs = new List<TestEventArgs>();

        var events = AsyncEnumerable.FromEvent<TestEventArgs>(
            h => eventSource.TestEvent += h,
            h => eventSource.TestEvent -= h, cancellationToken: token);

        // Act
        var enumerationTask = Task.Run(async () =>
        {
            await foreach (var evt in events)
            {
                receivedArgs.Add(evt);
                if (receivedArgs.Count >= 2)
                    break;
            }
        }, token);

        await Task.Delay(50, token);
        eventSource.RaiseTestEvent("Event1");
        eventSource.RaiseTestEvent("Event2");

        await enumerationTask;

        // Assert
        await Assert.That(receivedArgs.Count).IsEqualTo(2);
        await Assert.That(receivedArgs[0].Message).IsEqualTo("Event1");
        await Assert.That(receivedArgs[1].Message).IsEqualTo("Event2");
    }

    [Test]
    public async Task FromEvent_WithBoundedChannel_ShouldRespectCapacity(CancellationToken token)
    {
        // Arrange
        var eventSource = new TestEventSource();
        var receivedEvents = new List<int>();
        const int capacity = 3;

        var events = AsyncEnumerable.FromEvent<int>(
            h => eventSource.NumberEvent += h,
            h => eventSource.NumberEvent -= h,
            capacity: capacity, cancellationToken: token);

        // Act
        var enumerationTask = Task.Run(async () =>
        {
            await foreach (var evt in events)
            {
                receivedEvents.Add(evt);
                await Task.Delay(100, token); // Slow consumer
                if (receivedEvents.Count >= 5)
                    break;
            }
        }, token);

        await Task.Delay(50, token);
        
        // Rapidly raise events
        for (var i = 1; i <= 5; i++)
        {
            eventSource.RaiseNumber(i);
        }

        await enumerationTask;

        // Assert - all events should be received even with bounded channel
        await Assert.That(receivedEvents.Count).IsEqualTo(5);
        await Assert.That(receivedEvents).IsEquivalentTo([1, 2, 3, 4, 5]);
    }

    [Test]
    public async Task FromEvent_ShouldBeThreadSafe_WithConcurrentEvents(CancellationToken token)
    {
        // Arrange
        var eventSource = new TestEventSource();
        var receivedEvents = new List<int>();
        const int eventCount = 100;

        var events = AsyncEnumerable.FromEvent<int>(
            h => eventSource.NumberEvent += h,
            h => eventSource.NumberEvent -= h, cancellationToken: token);

        // Act
        var enumerationTask = Task.Run(async () =>
        {
            await foreach (var evt in events)
            {
                lock (receivedEvents)
                {
                    receivedEvents.Add(evt);
                    if (receivedEvents.Count >= eventCount)
                        break;
                }
            }
        }, token);

        await Task.Delay(50, token);

        // Raise events from multiple threads concurrently
        var tasks = Enumerable.Range(1, eventCount)
            .Select(i => Task.Run(() => eventSource.RaiseNumber(i), token))
            .ToArray();

        await Task.WhenAll(tasks);
        await enumerationTask;

        // Assert
        await Assert.That(receivedEvents.Count).IsEqualTo(eventCount);
        await Assert.That(receivedEvents.Distinct().Count()).IsEqualTo(eventCount);
    }

    [Test]
    public async Task FromEvent_ShouldThrowArgumentNullException_WhenSubscribeIsNull()
    {
        await Assert.That(TestAction).Throws<ArgumentNullException>();
        return;

        // Act & Assert
        async Task TestAction()
        {
            Action<Action<int>> subscribe = null!;
            Action<Action<int>> unsubscribe = _ => { };
            await AsyncEnumerable.FromEvent(subscribe, unsubscribe).FirstOrDefaultAsync();
        }
    }

    [Test]
    public async Task FromEvent_ShouldThrowArgumentNullException_WhenUnsubscribeIsNull()
    {
        await Assert.That(TestAction).Throws<ArgumentNullException>();
        return;

        // Act & Assert
        async Task TestAction()
        {
            Action<Action<int>> subscribe = _ => { };
            Action<Action<int>> unsubscribe = null!;
            await AsyncEnumerable.FromEvent(subscribe, unsubscribe).FirstOrDefaultAsync();
        }
    }

    [Test]
    public async Task FromEvent_ShouldHandleMultipleEnumerators(CancellationToken token)
    {
        // Arrange
        var eventSource = new TestEventSource();
        var receivedEvents1 = new List<int>();
        var receivedEvents2 = new List<int>();

        // Each call to FromEvent creates a new subscription
        var events1 = AsyncEnumerable.FromEvent<int>(
            h => eventSource.NumberEvent += h,
            h => eventSource.NumberEvent -= h, cancellationToken: token);
        
        var events2 = AsyncEnumerable.FromEvent<int>(
            h => eventSource.NumberEvent += h,
            h => eventSource.NumberEvent -= h, cancellationToken: token);

        // Act
        var task1 = Task.Run(async () =>
        {
            await foreach (var evt in events1)
            {
                receivedEvents1.Add(evt);
                if (receivedEvents1.Count >= 2)
                    break;
            }
        }, token);

        var task2 = Task.Run(async () =>
        {
            await foreach (var evt in events2)
            {
                receivedEvents2.Add(evt);
                if (receivedEvents2.Count >= 2)
                    break;
            }
        }, token);

        await Task.Delay(50, token);
        eventSource.RaiseNumber(1);
        eventSource.RaiseNumber(2);

        await Task.WhenAll(task1, task2);

        // Assert - both enumerators should receive events independently
        await Assert.That(receivedEvents1).IsEquivalentTo([1, 2]);
        await Assert.That(receivedEvents2).IsEquivalentTo([1, 2]);
    }

    [Test]
    public async Task FromEvent_ShouldCompleteCleanly_WhenNoEventsAreRaised()
    {
        // Arrange
        var eventSource = new TestEventSource();
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        var events = AsyncEnumerable.FromEvent<int>(
            h => eventSource.NumberEvent += h,
            h => eventSource.NumberEvent -= h
        );

        // Act & Assert - should complete via cancellation without throwing unexpected exceptions
        var exceptionThrown = false;
        try
        {
            var token = cts.Token;
            await foreach (var _ in events.WithCancellation(token))
            {
                // Should not reach here
            }
        }
        catch (OperationCanceledException)
        {
            // Expected - this is how the enumeration completes
        }
        catch (Exception)
        {
            exceptionThrown = true;
        }
        finally
        {
            cts.Dispose();
        }

        await Assert.That(exceptionThrown).IsFalse();
        await Assert.That(eventSource.HasNumberSubscribers).IsFalse();
    }
}

// Test helper classes
public class TestEventSource
{
    public event Action<int>? NumberEvent;
    public event EventHandler<TestEventArgs>? TestEvent;

    public bool HasNumberSubscribers => NumberEvent != null;
    public bool HasTestSubscribers => TestEvent != null;

    public void RaiseNumber(int value) => NumberEvent?.Invoke(value);
    
    public void RaiseTestEvent(string message)
    {
        TestEvent?.Invoke(this, new() { Message = message });
    }
}

public class TestEventArgs : EventArgs
{
    public required string Message { get; init; }
}
