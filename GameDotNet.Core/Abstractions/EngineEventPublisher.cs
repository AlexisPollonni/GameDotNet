using System.Numerics;
using MessagePipe;

namespace GameDotNet.Core.Abstractions;

public sealed class EngineEventPublisher(
    ISingletonPublisher<IViewPort, KeyDownEvent> keyDown,
    ISingletonPublisher<IViewPort, KeyUpEvent> keyUp,
    ISingletonPublisher<IViewPort, MouseClickDownEvent> mouseClickDown,
    ISingletonPublisher<IViewPort, MouseClickUpEvent> mouseClickUp,
    ISingletonPublisher<IViewPort, MouseScrollEvent> mouseScroll,
    ISingletonPublisher<IViewPort, MouseMoveEvent> mouseMove,
    
    //Viewport events
    IPublisher<ViewportCreatedEvent> created,
    IPublisher<ViewportDestroyedEvent> destroyed,
    IPublisher<ViewportActiveChangedEvent> active)
{
    public void SendKeyDown(IViewPort view, Key key) =>
        keyDown.Publish(view, new(key));

    public void SendKeyUp(IViewPort view, Key key) =>
        keyUp.Publish(view, new(key));

    public void SendMouseClickDown(IViewPort view, MouseButton button) =>
        mouseClickDown.Publish(view, new(button));

    public void SendMouseClickUp(IViewPort view, MouseButton button) =>
        mouseClickUp.Publish(view, new(button));

    public void SendMouseScroll(IViewPort view, Vector2 delta) =>
        mouseScroll.Publish(view, new(delta));

    public void SendMouseMove(IViewPort view, Vector2 delta) =>
        mouseMove.Publish(view, new(delta));
    
    public void SendCreated(IViewPort view) =>
        created.Publish(new(view));

    public void SendDestroyed(IViewPort view) =>
        destroyed.Publish(new(view));

    public void SendActiveChanged(IViewPort viewport)
    {
        active.Publish(new(_activeViewport, viewport));
        _activeViewport = viewport;
    }
    
    private IViewPort? _activeViewport;
}