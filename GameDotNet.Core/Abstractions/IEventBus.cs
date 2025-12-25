namespace GameDotNet.Core.Abstractions;

public interface IEventBus
{
    void Publish<TEvent>(TEvent evt);
    void Publish<TKey, TEvent>(TKey key, TEvent evt) where TKey : notnull;


    ValueTask PublishAsync<TEvent>(TEvent evt, CancellationToken cancellationToken = default);

    ValueTask PublishAsync<TKey, TEvent>(TKey key, TEvent evt, CancellationToken cancellationToken = default)
        where TKey : notnull;


    ValueTask PublishAllAsync<TEvent>(IAsyncEnumerable<TEvent> events, CancellationToken cancellationToken = default);

    ValueTask PublishAllAsync<TKey, TEvent>(TKey key, IAsyncEnumerable<TEvent> events,
        CancellationToken cancellationToken = default) where TKey : notnull;
}