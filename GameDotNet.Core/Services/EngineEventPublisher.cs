using System.Numerics;
using GameDotNet.Core.Abstractions;
using GameDotNet.Core.Models;

namespace GameDotNet.Core.Services;

[RegisterSingleton<EngineEventPublisher>]
public sealed class EngineEventPublisher(IEventBus eventBus)
{
    public void SendKeyDown(IViewPort view, Key key) =>
        eventBus.Publish(view, new KeyDownEvent(key));

    public void SendKeyUp(IViewPort view, Key key) =>
        eventBus.Publish(view, new KeyUpEvent(key));

    public void SendMouseClickDown(IViewPort view, MouseButton button) =>
        eventBus.Publish(view, new MouseClickDownEvent(button));

    public void SendMouseClickUp(IViewPort view, MouseButton button) =>
        eventBus.Publish(view, new MouseClickUpEvent(button));

    public void SendMouseScroll(IViewPort view, Vector2 delta) =>
        eventBus.Publish(view, new MouseScrollEvent(delta));

    public void SendMouseMove(IViewPort view, Vector2 delta) =>
        eventBus.Publish(view, new MouseMoveEvent(delta));
    
    public void SendCreated(IViewPort view) =>
        eventBus.Publish(new ViewportCreatedEvent(view));

    public void SendDestroyed(IViewPort view) =>
        eventBus.Publish(new ViewportDestroyedEvent(view));

    public void SendActiveChanged(IViewPort viewport)
    {
        eventBus.Publish(new ViewportActiveChangedEvent(_activeViewport, viewport));
        _activeViewport = viewport;
    }
    
    private IViewPort? _activeViewport;
}