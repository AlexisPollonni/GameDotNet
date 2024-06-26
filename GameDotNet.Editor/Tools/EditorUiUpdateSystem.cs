using System;
using GameDotNet.Management.ECS;
using MessagePipe;

namespace GameDotNet.Editor.Tools;

public class EditorUiUpdateSystem : SystemBase
{
    public IObservable<EditorUpdateEventArgs> SampledUpdate { get; }


    private readonly IDisposablePublisher<EditorUpdateEventArgs> _publisher;


    public EditorUiUpdateSystem(EventFactory factory) :
        base(new(int.MaxValue){UpdateThrottle = TimeSpan.FromSeconds(0.5)})
    {
        (_publisher, var subscriber) = factory.CreateEvent<EditorUpdateEventArgs>();
        
        SampledUpdate = subscriber.AsObservable();
    }

    public override void Update(TimeSpan delta) => _publisher.Publish(new());
    
    public readonly struct EditorUpdateEventArgs;
}