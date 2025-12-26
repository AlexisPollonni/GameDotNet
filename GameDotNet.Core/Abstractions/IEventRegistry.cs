namespace GameDotNet.Core.Abstractions;

public interface IEventRegistry
{
    public delegate Task EventRegistrationHandler<in TEvent>(IAsyncEnumerable<TEvent> evt, CancellationToken token = default);
    
    public ref struct EventSubscriptionBuilder
    {
        //TODO: options here in the future
    }
    
    EventSubscriptionBuilder On<TEvent>(EventRegistrationHandler<TEvent> handler, CancellationToken token = default);
    
    EventSubscriptionBuilder On<TKey, TEvent>(TKey key, EventRegistrationHandler<TEvent> handler, CancellationToken token = default) where TKey : notnull;
}