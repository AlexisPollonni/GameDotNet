using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace GameDotNet.Core.Tooling.Extensions;

public static class AsyncEnumerableTimeExtensions
{
    /// <summary>
    /// Throttles the stream to emit at most one item per time window.
    /// The first item in each window is emitted immediately, subsequent items are dropped.
    /// </summary>
    /// <remarks>
    /// Example: If duration is 100ms and 5 items arrive at t=0, t=10, t=50, t=150, t=200:
    /// - Item at t=0 emitted immediately (starts window)
    /// - Items at t=10, t=50 dropped (within 100ms window)
    /// - Item at t=150 emitted (new window)
    /// - Item at t=200 dropped (within window)
    /// </remarks>
    public static async IAsyncEnumerable<T> Throttle<T>(
        this IAsyncEnumerable<T> source,
        TimeSpan duration,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var lastEmitTime = DateTimeOffset.MinValue;

        await foreach (var item in source.WithCancellation(cancellationToken))
        {
            var now = DateTimeOffset.UtcNow;
            var elapsed = now - lastEmitTime;

            if (elapsed >= duration)
            {
                lastEmitTime = now;
                yield return item;
            }
            // Otherwise drop the item
        }
    }

    /// <summary>
    /// Debounces the stream - emits an item only after the specified duration has passed
    /// without any new items arriving. Resets the timer on each new item.
    /// </summary>
    /// <remarks>
    /// Useful for search-as-you-type scenarios. If duration is 300ms:
    /// - User types "h" -> timer starts
    /// - User types "e" at t=50ms -> timer resets
    /// - User types "l" at t=100ms -> timer resets
    /// - User types "l" at t=150ms -> timer resets
    /// - User types "o" at t=200ms -> timer resets
    /// - At t=500ms (300ms after last input) -> emit "hello"
    /// </remarks>
    public static async IAsyncEnumerable<T> Debounce<T>(
        this IAsyncEnumerable<T> source,
        TimeSpan duration,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateUnbounded<T>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });

        var sourceTask = Task.Run(async () =>
        {
            await foreach (var item in source.WithCancellation(cancellationToken))
            {
                await channel.Writer.WriteAsync(item, cancellationToken);
            }
            channel.Writer.Complete();
        }, cancellationToken);

        T? lastItem = default;
        var hasItem = false;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        
        while (await channel.Reader.WaitToReadAsync(cancellationToken))
        {
            // Drain all pending items, keeping only the last one
            while (channel.Reader.TryRead(out var item))
            {
                lastItem = item;
                hasItem = true;
            }

            if (hasItem)
            {
                // Wait for the debounce duration
                var delayTask = Task.Delay(duration, cancellationToken);
                var readTask = channel.Reader.WaitToReadAsync(cancellationToken).AsTask();
                
                var completed = await Task.WhenAny(delayTask, readTask);
                
                // If delay completed first (no new items), emit the item
                if (completed == delayTask && !channel.Reader.TryPeek(out _))
                {
                    yield return lastItem!;
                    hasItem = false;
                }
                // Otherwise, new items arrived, loop to drain them
            }
        }

        await sourceTask;
    }

    /// <summary>
    /// Samples the stream at regular intervals, emitting the most recent item.
    /// If no items arrived in an interval, nothing is emitted for that interval.
    /// </summary>
    /// <remarks>
    /// Example: If interval is 100ms and items arrive at t=10, t=50, t=150:
    /// - At t=100ms: emit item from t=50 (most recent in first interval)
    /// - At t=200ms: emit item from t=150 (most recent in second interval)
    /// </remarks>
    public static async IAsyncEnumerable<T> Sample<T>(
        this IAsyncEnumerable<T> source,
        TimeSpan interval,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateUnbounded<T>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = true
        });

        var sourceTask = Task.Run(async () =>
        {
            await foreach (var item in source.WithCancellation(cancellationToken))
            {
                await channel.Writer.WriteAsync(item, cancellationToken);
            }
            channel.Writer.Complete();
        }, cancellationToken);

        using var timer = new PeriodicTimer(interval);
        T? lastItem = default;
        var hasItem = false;

        try
        {
            while (true)
            {
                // Drain all items until next tick
                var tickTask = timer.WaitForNextTickAsync(cancellationToken);
                var readTask = channel.Reader.Completion;

                var completedTask = await Task.WhenAny(tickTask.AsTask(), readTask);

                if (completedTask == readTask)
                {
                    // Source completed
                    break;
                }

                // Drain any pending items
                while (channel.Reader.TryRead(out var item))
                {
                    lastItem = item;
                    hasItem = true;
                }

                if (hasItem)
                {
                    yield return lastItem!;
                    hasItem = false;
                }
            }
        }
        finally
        {
            await sourceTask;
        }
    }

    /// <summary>
    /// Buffers items for the specified duration or until count is reached, whichever comes first.
    /// Emits the buffer when either condition is met.
    /// </summary>
    public static async IAsyncEnumerable<IReadOnlyList<T>> Buffer<T>(
        this IAsyncEnumerable<T> source,
        TimeSpan duration,
        int count,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var buffer = new List<T>(count);
        var lastFlush = DateTimeOffset.UtcNow;

        await foreach (var item in source.WithCancellation(cancellationToken))
        {
            buffer.Add(item);

            var shouldFlushByCount = buffer.Count >= count;
            var shouldFlushByTime = DateTimeOffset.UtcNow - lastFlush >= duration;

            if (shouldFlushByCount || shouldFlushByTime)
            {
                yield return buffer.ToArray(); // Return immutable copy
                buffer.Clear();
                lastFlush = DateTimeOffset.UtcNow;
            }
        }

        // Flush remaining items
        if (buffer.Count > 0)
        {
            yield return buffer.ToArray();
        }
    }

    /// <summary>
    /// Delays each item by the specified duration.
    /// </summary>
    public static async IAsyncEnumerable<T> Delay<T>(
        this IAsyncEnumerable<T> source,
        TimeSpan duration,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in source.WithCancellation(cancellationToken))
        {
            await Task.Delay(duration, cancellationToken);
            yield return item;
        }
    }

    /// <summary>
    /// Adds a timeout to the enumeration. Throws TimeoutException if no item arrives within the duration.
    /// </summary>
    public static async IAsyncEnumerable<T> Timeout<T>(
        this IAsyncEnumerable<T> source,
        TimeSpan duration,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var enumerator = source.GetAsyncEnumerator(cancellationToken);

        while (true)
        {
            using var timeoutCts = new CancellationTokenSource(duration);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            
            var moveNextTask = enumerator.MoveNextAsync().AsTask();
            var delayTask = Task.Delay(duration, linkedCts.Token);
            
            var completedTask = await Task.WhenAny(moveNextTask, delayTask);
            
            if (completedTask == delayTask && !moveNextTask.IsCompleted)
            {
                throw new TimeoutException($"No item received within {duration}");
            }

            bool hasNext = await moveNextTask;
            
            if (!hasNext)
                yield break;

            yield return enumerator.Current;
        }
    }
}