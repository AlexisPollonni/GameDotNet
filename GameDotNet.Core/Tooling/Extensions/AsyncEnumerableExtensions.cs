using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace GameDotNet.Core.Tooling.Extensions;

[SuppressMessage("ReSharper", "InvokeAsExtensionMember")]
public static class AsyncEnumerableExtensions
{
    extension(AsyncEnumerable)
    {
        /// <summary>
        /// Converts .NET events to an async enumerable sequence.
        /// Uses a Channel-based implementation for high performance and minimal allocations.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method provides a zero-allocation event handler approach using System.Threading.Channels,
        /// which is optimized for producer-consumer scenarios. The implementation is thread-safe and
        /// supports cancellation via the async enumerable's cancellation token.
        /// </para>
        /// <para>
        /// Performance characteristics:
        /// - Unbounded channels (default): Lock-free TryWrite operations, minimal allocations
        /// - Bounded channels: May block producers when full (using BoundedChannelFullMode.Wait by default or as specified)
        /// - Event handlers execute synchronously without Task.Run overhead
        /// - Proper cleanup via finally block ensures event unsubscription
        /// </para>
        /// <para>
        /// Example usage:
        /// <code>
        /// var timer = new System.Timers.Timer(100);
        /// var events = AsyncEnumerable.FromEvent&lt;ElapsedEventArgs&gt;(
        ///     h => timer.Elapsed += h,
        ///     h => timer.Elapsed -= h
        /// );
        /// 
        /// timer.Start();
        /// await foreach (var evt in events.WithCancellation(cts.Token))
        /// {
        ///     Console.WriteLine($"Timer elapsed at {evt.SignalTime}");
        /// }
        /// </code>
        /// </para>
        /// </remarks>
        /// <typeparam name="T">The type of event data.</typeparam>
        /// <param name="subscribe">Action to subscribe an event handler.</param>
        /// <param name="unsubscribe">Action to unsubscribe an event handler.</param>
        /// <param name="capacity">Optional capacity for the internal buffer. Use -1 (default) for unbounded, or a positive number to limit buffering.</param>
        /// <param name="isSingleWriter">
        /// Set to true ONLY if you can guarantee the event will never fire concurrently from multiple threads.
        /// Use false (default) unless you're certain.
        /// </param>
        /// <param name="fullMode">if capacity is not negative, configures the built-in channel's behavior when full</param>
        /// <param name="cancellationToken">Cancels the subscription</param>
        /// <returns>An async enumerable that yields event data as it arrives.</returns>
        public static async IAsyncEnumerable<T> FromEvent<T>(
            Action<Action<T>> subscribe,
            Action<Action<T>> unsubscribe,
            int capacity = -1,
            bool isSingleWriter = false,
            BoundedChannelFullMode fullMode = BoundedChannelFullMode.Wait,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(subscribe);
            ArgumentNullException.ThrowIfNull(unsubscribe);

            // need a channel to convert from push-based to pull-based events
            Channel<T> channel;

            if (capacity > 0)
            {
                var options = new BoundedChannelOptions(capacity)
                {
                    FullMode = fullMode,
                    SingleReader = true,
                    SingleWriter = isSingleWriter
                };
                channel = Channel.CreateBounded<T>(options);
            }
            else
            {
                var options = new UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = isSingleWriter
                };
                channel = Channel.CreateUnbounded<T>(options);
            }

            var writer = channel.Writer;

            subscribe(Handler);

            await using var registration = cancellationToken.Register(() =>
            {
                unsubscribe(Handler);
                writer.TryComplete();
            });


            try
            {
                await foreach (var item in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
                {
                    yield return item;
                }
            }
            finally
            {
                unsubscribe(Handler);
                writer.TryComplete();
            }


            yield break;

            void Handler(T item)
            {
#pragma warning disable SYSLIB5007
#pragma warning disable CA2252
                AsyncHelpers.Await(writer.WriteAsync(item, cancellationToken));
#pragma warning restore SYSLIB5007
#pragma warning restore CA2252
            }
        }

        /// <summary>
        /// Converts .NET events with EventHandler&lt;TEventArgs&gt; pattern to an async enumerable sequence.
        /// </summary>
        /// <remarks>
        /// This is a convenience overload for the standard .NET event pattern with EventHandler&lt;TEventArgs&gt;.
        /// The sender parameter is discarded, and only the event args are yielded.
        /// </remarks>
        /// <typeparam name="TEventArgs">The type of event arguments.</typeparam>
        /// <param name="subscribe">Action to subscribe an event handler.</param>
        /// <param name="unsubscribe">Action to unsubscribe an event handler.</param>
        /// <param name="capacity">Optional capacity for the internal buffer. Use -1 (default) for unbounded.</param>
        /// <param name="isSingleWriter">
        /// Set to true ONLY if you can guarantee the event will never fire concurrently from multiple threads.
        /// Use false (default) unless you're certain.
        /// </param>
        /// <param name="fullMode">if capacity is not negative, configures the built-in channel's behavior when full</param>
        /// <param name="cancellationToken">Cancels the subscription</param>
        /// <returns>An async enumerable that yields event arguments as they arrive.</returns>
        public static IAsyncEnumerable<TEventArgs> FromEvent<TEventArgs>(
            Action<EventHandler<TEventArgs>> subscribe,
            Action<EventHandler<TEventArgs>> unsubscribe,
            int capacity = -1,
            bool isSingleWriter = false,
            BoundedChannelFullMode fullMode = BoundedChannelFullMode.Wait,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(subscribe);
            ArgumentNullException.ThrowIfNull(unsubscribe);

            return FromEvent<TEventArgs>(
                handler => subscribe((_, args) => handler(args)),
                handler => unsubscribe((_, args) => handler(args)),
                capacity, isSingleWriter, fullMode, cancellationToken);
        }
    }
}