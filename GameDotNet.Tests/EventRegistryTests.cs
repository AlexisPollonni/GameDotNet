using System.Diagnostics;
using GameDotNet.Core.Abstractions;
using GameDotNet.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GameDotNet.Tests;

public class MicrosoftDependencyInjectionDataSourceAttribute : DependencyInjectionDataSourceAttribute<IServiceScope>
{
    private static readonly IServiceProvider ServiceProvider = CreateSharedServiceProvider();

    public override IServiceScope CreateScope(DataGeneratorMetadata dataGeneratorMetadata)
    {
        return ServiceProvider.CreateScope();
    }

    public override object? Create(IServiceScope scope, Type type)
    {
        return scope.ServiceProvider.GetService(type);
    }
    
    private static IServiceProvider CreateSharedServiceProvider()
    {
        return new ServiceCollection()
            .AddLogging()
            .AddSingleton(TimeProvider.System)
            .AddMessagePipe()
            .Services
            .AddSingleton<IEventRegistry, EventRegistry>()
            .AddSingleton<IEventBus>(provider => (IEventBus)provider.GetRequiredService<IEventRegistry>())
            .AddHostedService(sp => (EventRegistry)sp.GetRequiredService<IEventRegistry>())
            .AddSingleton(typeof(IZeroAllocThreadPoolScheduler<>), typeof(PooledThreadPoolScheduler<>))
            .BuildServiceProvider();
    }
}

[MicrosoftDependencyInjectionDataSource]
public class EventRegistryTests(IEventRegistry registry, IEventBus bus)
{
    // Test event types
    private record TestEvent(int Value);
    private record KeyedTestEvent(string Message);
    private record MultipleSubscribersEvent(int Id);
    private enum TestEventKey { Key1, Key2, Key3 }

    [Test]
    public async Task Publish_ShouldDeliverEvent_ToSingleSubscriber(CancellationToken token)
    {
        var receivedEvents = new List<TestEvent>();
        var tcs = new TaskCompletionSource();

        // Act
        registry.On<TestEvent>(async (events, ct) =>
        {
            await foreach (var evt in events.WithCancellation(ct))
            {
                receivedEvents.Add(evt);
                if (receivedEvents.Count >= 3)
                {
                    tcs.SetResult();
                    break;
                }
            }
        }, token);

        // Give time for subscription to be registered
        await Task.Delay(100, token);

        bus.Publish(new TestEvent(1));
        bus.Publish(new TestEvent(2));
        bus.Publish(new TestEvent(3));

        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5), token);

        // Assert
        await Assert.That(receivedEvents.Count).IsEqualTo(3);
        await Assert.That(receivedEvents[0].Value).IsEqualTo(1);
        await Assert.That(receivedEvents[1].Value).IsEqualTo(2);
        await Assert.That(receivedEvents[2].Value).IsEqualTo(3);
    }

    [Test]
    public async Task Publish_ShouldDeliverEvent_ToMultipleSubscribers(CancellationToken token)
    {
        // Arrange
        var receivedEvents1 = new List<MultipleSubscribersEvent>();
        var receivedEvents2 = new List<MultipleSubscribersEvent>();
        var tcs1 = new TaskCompletionSource();
        var tcs2 = new TaskCompletionSource();

        // Act
        registry.On<MultipleSubscribersEvent>(async (events, ct) =>
        {
            await foreach (var evt in events.WithCancellation(ct))
            {
                receivedEvents1.Add(evt);
                if (receivedEvents1.Count >= 2)
                {
                    tcs1.SetResult();
                    break;
                }
            }
        }, token);

        registry.On<MultipleSubscribersEvent>(async (events, ct) =>
        {
            await foreach (var evt in events.WithCancellation(ct))
            {
                receivedEvents2.Add(evt);
                if (receivedEvents2.Count >= 2)
                {
                    tcs2.SetResult();
                    break;
                }
            }
        }, token);

        await Task.Delay(100, token);

        bus.Publish(new MultipleSubscribersEvent(1));
        bus.Publish(new MultipleSubscribersEvent(2));

        await Task.WhenAll(tcs1.Task, tcs2.Task).WaitAsync(TimeSpan.FromSeconds(5), token);

        // Assert
        await Assert.That(receivedEvents1.Count).IsEqualTo(2);
        await Assert.That(receivedEvents2.Count).IsEqualTo(2);
        await Assert.That(receivedEvents1[0].Id).IsEqualTo(1);
        await Assert.That(receivedEvents2[0].Id).IsEqualTo(1);
    }

    [Test]
    public async Task PublishKeyed_ShouldDeliverEvent_OnlyToMatchingKeySubscriber(CancellationToken token)
    {
        // Arrange
        var receivedKey1 = new List<KeyedTestEvent>();
        var receivedKey2 = new List<KeyedTestEvent>();
        var tcs1 = new TaskCompletionSource();
        var tcs2 = new TaskCompletionSource();

        // Act
        registry.On<TestEventKey, KeyedTestEvent>(TestEventKey.Key1, async (events, ct) =>
        {
            await foreach (var evt in events.WithCancellation(ct))
            {
                receivedKey1.Add(evt);
                tcs1.SetResult();
                break;
            }
        }, token);

        registry.On<TestEventKey, KeyedTestEvent>(TestEventKey.Key2, async (events, ct) =>
        {
            await foreach (var evt in events.WithCancellation(ct))
            {
                receivedKey2.Add(evt);
                tcs2.SetResult();
                break;
            }
        }, token);

        await Task.Delay(100, token);

        bus.Publish(TestEventKey.Key1, new KeyedTestEvent("Message for Key1"));
        bus.Publish(TestEventKey.Key2, new KeyedTestEvent("Message for Key2"));

        await Task.WhenAll(tcs1.Task, tcs2.Task).WaitAsync(TimeSpan.FromSeconds(5), token);

        // Assert
        await Assert.That(receivedKey1.Count).IsEqualTo(1);
        await Assert.That(receivedKey2.Count).IsEqualTo(1);
        await Assert.That(receivedKey1[0].Message).IsEqualTo("Message for Key1");
        await Assert.That(receivedKey2[0].Message).IsEqualTo("Message for Key2");
    }

    [Test]
    public async Task PublishKeyed_ShouldNotDeliverEvent_ToWrongKeySubscriber(CancellationToken token)
    {
        // Arrange
        var receivedEvents = new List<KeyedTestEvent>();

        // Act
        registry.On<TestEventKey, KeyedTestEvent>(TestEventKey.Key1, async (events, ct) =>
        {
            await foreach (var evt in events.WithCancellation(ct))
            {
                receivedEvents.Add(evt);
                break;
            }
        }, token);

        await Task.Delay(100, token);

        // Publish to a different key
        bus.Publish(TestEventKey.Key2, new KeyedTestEvent("Message for Key2"));
        bus.Publish(TestEventKey.Key3, new KeyedTestEvent("Message for Key3"));

        // Wait a bit to ensure no events are received
        await Task.Delay(300, token);

        // Assert
        await Assert.That(receivedEvents.Count).IsEqualTo(0);
    }

    [Test]
    public async Task PublishAsync_ShouldDeliverEvent_ToSubscriber(CancellationToken token)
    {
        // Arrange
        var receivedEvents = new List<TestEvent>();
        var tcs = new TaskCompletionSource();

        // Act
        registry.On<TestEvent>(async (events, ct) =>
        {
            await foreach (var evt in events.WithCancellation(ct))
            {
                receivedEvents.Add(evt);
                tcs.SetResult();
                break;
            }
        }, token);

        await Task.Delay(100, token);
        await bus.PublishAsync(new TestEvent(42), token);
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5), token);

        // Assert
        await Assert.That(receivedEvents.Count).IsEqualTo(1);
        await Assert.That(receivedEvents[0].Value).IsEqualTo(42);
    }

    [Test]
    public async Task PublishAllAsync_ShouldDeliverAllEvents_ToSubscriber(CancellationToken token)
    {
        // Arrange
        var receivedEvents = new List<TestEvent>();
        var tcs = new TaskCompletionSource();

        // Act
        registry.On<TestEvent>(async (events, ct) =>
        {
            await foreach (var evt in events.WithCancellation(ct))
            {
                receivedEvents.Add(evt);
                if (receivedEvents.Count >= 5)
                {
                    tcs.SetResult();
                    break;
                }
            }
        }, token);

        await Task.Delay(100, token);

        var eventsToPublish = AsyncEnumerable.Range(1, 5).Select(i => new TestEvent(i));
        await bus.PublishAllAsync(eventsToPublish, token);
        
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5), token);

        // Assert
        await Assert.That(receivedEvents.Count).IsEqualTo(5);
        for (int i = 0; i < 5; i++)
        {
            await Assert.That(receivedEvents[i].Value).IsEqualTo(i + 1);
        }
    }

    [Test]
    public async Task PublishAllAsync_WithKey_ShouldDeliverAllEvents_ToSubscriber(CancellationToken token)
    {
        // Arrange
        var receivedEvents = new List<KeyedTestEvent>();
        var tcs = new TaskCompletionSource();

        // Act
        registry.On<TestEventKey, KeyedTestEvent>(TestEventKey.Key1, async (events, ct) =>
        {
            await foreach (var evt in events.WithCancellation(ct))
            {
                receivedEvents.Add(evt);
                if (receivedEvents.Count >= 3)
                {
                    tcs.SetResult();
                    break;
                }
            }
        }, token);

        await Task.Delay(100, token);

        var eventsToPublish = AsyncEnumerable.Range(1, 3)
            .Select(i => new KeyedTestEvent($"Message {i}"));
        await bus.PublishAllAsync(TestEventKey.Key1, eventsToPublish, token);
        
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5), token);

        // Assert
        await Assert.That(receivedEvents.Count).IsEqualTo(3);
        await Assert.That(receivedEvents[0].Message).IsEqualTo("Message 1");
        await Assert.That(receivedEvents[1].Message).IsEqualTo("Message 2");
        await Assert.That(receivedEvents[2].Message).IsEqualTo("Message 3");
    }

    [Test]
    public async Task Subscriber_ShouldStopReceivingEvents_WhenBreakingFromLoop(CancellationToken token)
    {
        // Arrange
        var receivedEvents = new List<TestEvent>();
        var tcs = new TaskCompletionSource();

        // Act
        registry.On<TestEvent>(async (events, ct) =>
        {
            await foreach (var evt in events.WithCancellation(ct))
            {
                receivedEvents.Add(evt);
                if (receivedEvents.Count >= 2)
                {
                    tcs.SetResult();
                    break; // Stop listening after 2 events
                }
            }
        }, token);

        await Task.Delay(100, token);

        bus.Publish(new TestEvent(1));
        bus.Publish(new TestEvent(2));
        
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
        
        // Publish more events after breaking
        bus.Publish(new TestEvent(3));
        bus.Publish(new TestEvent(4));
        
        await Task.Delay(200, token);

        // Assert - should only have received the first 2 events
        await Assert.That(receivedEvents.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Subscriber_ShouldHandleCancellation_Gracefully(CancellationToken token)
    {
        // Arrange
        var receivedEvents = new List<TestEvent>();
        var cts = new CancellationTokenSource();
        var subscriptionCancelled = false;
        var tcs = new TaskCompletionSource();

        // Act
        registry.On<TestEvent>(async (events, ct) =>
        {
            try
            {
                await foreach (var evt in events.WithCancellation(ct))
                {
                    receivedEvents.Add(evt);
                    if (receivedEvents.Count == 1)
                    {
                        cts.Cancel(); // Cancel after first event
                    }
                }
            }
            catch (OperationCanceledException)
            {
                subscriptionCancelled = true;
                tcs.SetResult();
            }
        }, cts.Token);

        await Task.Delay(100, token);

        bus.Publish(new TestEvent(1));
        
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5), token);

        // Assert
        await Assert.That(subscriptionCancelled).IsTrue();
        await Assert.That(receivedEvents.Count).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task Subscriber_WithException_ShouldNotCrashOtherSubscribers(CancellationToken token)
    {
        // Arrange
        var receivedEvents = new List<TestEvent>();
        var tcs = new TaskCompletionSource();

        // Act - First subscriber throws exception
        registry.On<TestEvent>(async (events, ct) =>
        {
            await foreach (var evt in events.WithCancellation(ct))
            {
                throw new InvalidOperationException("Test exception");
            }
        }, token);

        // Second subscriber should still work
        registry.On<TestEvent>(async (events, ct) =>
        {
            await foreach (var evt in events.WithCancellation(ct))
            {
                receivedEvents.Add(evt);
                tcs.SetResult();
                break;
            }
        }, token);

        await Task.Delay(100, token);
        bus.Publish(new TestEvent(1));
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5), token);

        // Assert
        await Assert.That(receivedEvents.Count).IsEqualTo(1);
    }

    [Test]
    public async Task MultipleKeys_ShouldIsolateEvents_Correctly(CancellationToken token)
    {
        // Arrange
        var receivedKey1 = new List<KeyedTestEvent>();
        var receivedKey2 = new List<KeyedTestEvent>();
        var receivedKey3 = new List<KeyedTestEvent>();
        var tcs1 = new TaskCompletionSource();
        var tcs2 = new TaskCompletionSource();
        var tcs3 = new TaskCompletionSource();

        // Act
        registry.On<TestEventKey, KeyedTestEvent>(TestEventKey.Key1, async (events, ct) =>
        {
            await foreach (var evt in events.WithCancellation(ct))
            {
                receivedKey1.Add(evt);
                if (receivedKey1.Count >= 2) { tcs1.SetResult(); break; }
            }
        }, token);

        registry.On<TestEventKey, KeyedTestEvent>(TestEventKey.Key2, async (events, ct) =>
        {
            await foreach (var evt in events.WithCancellation(ct))
            {
                receivedKey2.Add(evt);
                tcs2.SetResult();
                break;
            }
        }, token);

        registry.On<TestEventKey, KeyedTestEvent>(TestEventKey.Key3, async (events, ct) =>
        {
            await foreach (var evt in events.WithCancellation(ct))
            {
                receivedKey3.Add(evt);
                if (receivedKey3.Count >= 3) { tcs3.SetResult(); break; }
            }
        }, token);

        await Task.Delay(100, token);

        bus.Publish(TestEventKey.Key1, new KeyedTestEvent("Key1-1"));
        bus.Publish(TestEventKey.Key3, new KeyedTestEvent("Key3-1"));
        bus.Publish(TestEventKey.Key1, new KeyedTestEvent("Key1-2"));
        bus.Publish(TestEventKey.Key2, new KeyedTestEvent("Key2-1"));
        bus.Publish(TestEventKey.Key3, new KeyedTestEvent("Key3-2"));
        bus.Publish(TestEventKey.Key3, new KeyedTestEvent("Key3-3"));

        await Task.WhenAll(tcs1.Task, tcs2.Task, tcs3.Task).WaitAsync(TimeSpan.FromSeconds(5), token);

        // Assert
        await Assert.That(receivedKey1.Count).IsEqualTo(2);
        await Assert.That(receivedKey2.Count).IsEqualTo(1);
        await Assert.That(receivedKey3.Count).IsEqualTo(3);
        await Assert.That(receivedKey1[0].Message).IsEqualTo("Key1-1");
        await Assert.That(receivedKey2[0].Message).IsEqualTo("Key2-1");
        await Assert.That(receivedKey3[2].Message).IsEqualTo("Key3-3");
    }

    [Test]
    public async Task DisposeAsync_ShouldWaitForListeners_WithTimeout(CancellationToken token)
    {
        // Arrange
        var listenerStarted = new TaskCompletionSource();

        // Act
        registry.On<TestEvent>(async (events, ct) =>
        {
            await foreach (var evt in events.WithCancellation(ct))
            {
                listenerStarted.SetResult();
                // Simulate long-running listener
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
            }
        }, token);

        await Task.Delay(100, token);
        bus.Publish(new TestEvent(1));
        await listenerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), token);

        // Dispose should timeout waiting for the listener
        var stopwatch = Stopwatch.StartNew();
        await ((IAsyncDisposable)registry).DisposeAsync();
        stopwatch.Stop();

        // Assert - should timeout around 5 seconds (configured in EventRegistry)
        await Assert.That(stopwatch.Elapsed).IsLessThan(TimeSpan.FromSeconds(7));
    }

    [Test]
    public async Task NoSubscribers_ShouldNotCrash_WhenPublishing(CancellationToken token)
    {
        // Act & Assert - should not throw
        bus.Publish(new TestEvent(1));
        await bus.PublishAsync(new TestEvent(2), token);
        
        var events = AsyncEnumerable.Range(1, 3).Select(i => new TestEvent(i));
        await bus.PublishAllAsync(events, token);
        
        // Give time for potential errors
        await Task.Delay(100, token);
    }

    [Test]
    public async Task PublishAsync_WithNullKey_ShouldThrow(CancellationToken token)
    {
        // Act & Assert
        await Assert.That(async () => await bus.PublishAsync<string?, TestEvent>(null, new TestEvent(1), token))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Publish_WithNullKey_ShouldThrow(CancellationToken token)
    {
        // Act & Assert
        await Assert.That(() => bus.Publish<string?, TestEvent>(null!, new TestEvent(1)))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task On_WithNullHandler_ShouldThrow(CancellationToken token)
    {
        // Act & Assert
        await Assert.That(() => registry.On<TestEvent>(null!, token))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task OnKeyed_WithNullHandler_ShouldThrow(CancellationToken token)
    {
        // Act & Assert
        await Assert.That(() => registry.On<TestEventKey, KeyedTestEvent>(TestEventKey.Key1, null!, token))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task OnKeyed_WithNullKey_ShouldThrow(CancellationToken token)
    {
        // Act & Assert
        await Assert.That(() => registry.On<string?, TestEvent>(null!, async (events, ct) =>
        {
            await foreach (var evt in events.WithCancellation(ct)) { }
        }, token))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task PublishAllAsync_WithNullEvents_ShouldThrow(CancellationToken token)
    {
        // Act & Assert
        await Assert.That(async () => await bus.PublishAllAsync<TestEvent>(null!, token))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task PublishAllAsyncKeyed_WithNullEvents_ShouldThrow(CancellationToken token)
    {
        // Act & Assert
        await Assert.That(async () => 
            await bus.PublishAllAsync<TestEventKey, KeyedTestEvent>(TestEventKey.Key1, null!, token))
            .Throws<ArgumentNullException>();
    }
}

