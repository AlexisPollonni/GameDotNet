namespace GameDotNet.Core.Abstractions;

public interface IEventRegistry
{
    public ref struct EventSubscriptionBuilder
    {
        //TODO: options here in the future
    }
    
    EventSubscriptionBuilder On<TEvent>(Func<IAsyncEnumerable<TEvent>, CancellationToken, Task> handler, CancellationToken token = default);
    
    EventSubscriptionBuilder On<TKey, TEvent>(TKey key, Func<IAsyncEnumerable<TEvent>, CancellationToken, Task> handler, CancellationToken token = default) where TKey : notnull;
}