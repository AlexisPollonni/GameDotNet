using GameDotNet.Core.Abstractions;

namespace GameDotNet.Editor.Services;


[RegisterSingleton(Registration = RegistrationStrategy.Self)]
public partial class EditorSampledUpdatePublisher(IEventBus eventBus) : IUpdateJob
{    
    public ValueTask OnUpdate(TimeSpan deltaTime, CancellationToken cancellationToken = default)
    {
        eventBus.Publish(new EditorUpdateEventArgs());
        
        return default;
    }

    public bool IsStarted { get; set; }

    public JobConfiguration Options { get; } = new() { UpdateThrottle = TimeSpan.FromSeconds(0.5) };
}
internal readonly record struct EditorUpdateEventArgs;