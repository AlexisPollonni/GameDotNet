using GameDotNet.Core.Abstractions;
using static GameDotNet.Core.Abstractions.IEventRegistry;

namespace GameDotNet.Core.Tooling.Extensions;

public static class EventRegistryExtensions
{
    extension(IEventRegistry registry)
    {
        public EventSubscriptionBuilder OnEvent<TEvent>(Func<TEvent, CancellationToken, ValueTask> handler,
            CancellationToken token = default)
        {
            //TODO: TState overloads might be needed to avoid boxing, to profile
            return registry.On<TEvent>(async (evt, cancellationToken) =>
            {
                await foreach (var e in evt.WithCancellation(cancellationToken))
                {
                    await handler(e, cancellationToken);
                }
            }, token);
        }

        public EventSubscriptionBuilder OnEvent<TKey, TEvent>(TKey key, Func<TEvent, CancellationToken, ValueTask> handler,
            CancellationToken token = default) where TKey : notnull
        {
            return registry.On<TKey, TEvent>(key, Handler, token);
            
            async Task Handler(IAsyncEnumerable<TEvent> evt, CancellationToken cancellationToken)
            {
                await foreach (var e in evt.WithCancellation(cancellationToken))
                {
                    await handler(e, cancellationToken);
                }
            }
        }
    }
}