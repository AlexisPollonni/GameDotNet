using GameDotNet.Core.Abstractions;
using MessagePipe;

namespace GameDotNet.Editor.Tools;

public class EditorSampledUpdatePublisher(
    IPublisher<EditorSampledUpdatePublisher.EditorUpdateEventArgs> publisher,
    ISubscriber<EditorSampledUpdatePublisher.EditorUpdateEventArgs> subscriber) : IUpdateJob
{
    public IObservable<EditorUpdateEventArgs> SampledUpdate { get; } = subscriber.AsObservable();

    public ValueTask OnUpdate(TimeSpan deltaTime, CancellationToken cancellationToken = default)
    {
        publisher.Publish(new());
        
        return default;
    }

    public bool IsStarted { get; set; }

    public JobConfiguration Options { get; } = new() { UpdateThrottle = TimeSpan.FromSeconds(0.5) };

    
    public readonly record struct EditorUpdateEventArgs;
}