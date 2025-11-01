using System.Numerics;
using Arch.Core;
using MessagePipe;

namespace GameDotNet.Core.Abstractions;

public readonly record struct EngineStartedEvent;
public readonly record struct EngineStoppingEvent;
public readonly record struct EntityCreatedEvent(Entity New);
public readonly record struct EntityDestroyedEvent(Entity Destroyed);
public readonly record struct EntityComponentAddedEvent(Entity Entity, ComponentType Type);
public readonly record struct EntityComponentSetEvent(Entity Entity, ComponentType Type);
public readonly record struct EntityComponentRemovedEvent(Entity Entity, ComponentType Type);
public readonly record struct ViewportCreatedEvent(IViewPort View);
public readonly record struct ViewportDestroyedEvent(IViewPort Viewport);
public readonly record struct ActiveViewportChangedEvent(IViewPort? Previous, IViewPort Current);
public readonly record struct KeyDownEvent(Key Key);
public readonly record struct KeyUpEvent(Key Key);
public readonly record struct MouseClickDownEvent(MouseButton Button);
public readonly record struct MouseClickUpEvent(MouseButton Button);
public readonly record struct MouseScrollEvent(Vector2 Delta);
public readonly record struct MouseMoveEvent(Vector2 Delta);

public sealed class InputPublisher(
    IPublisher<IViewPort, KeyDownEvent> keyDown,
    IPublisher<IViewPort, KeyUpEvent> keyUp,
    IPublisher<IViewPort, MouseClickDownEvent> mouseClickDown,
    IPublisher<IViewPort, MouseClickUpEvent> mouseClickUp,
    IPublisher<IViewPort, MouseScrollEvent> mouseScroll,
    IPublisher<IViewPort, MouseMoveEvent> mouseMove)
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
}

public sealed class ViewPortPublisher(
    IPublisher<ViewportCreatedEvent> created,
    IPublisher<ViewportDestroyedEvent> destroyed,
    IPublisher<ActiveViewportChangedEvent> active)
{
    private IViewPort? _activeViewport;
    
    public void SendCreated(IViewPort view) =>
        created.Publish(new(view));

    public void SendDestroyed(IViewPort view) =>
        destroyed.Publish(new(view));

    public void SendActiveChanged(IViewPort viewport)
    {
        active.Publish(new(_activeViewport, viewport));
        _activeViewport = viewport;
    }
}