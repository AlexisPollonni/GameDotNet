using System.Numerics;
using MessagePipe;
using Silk.NET.Input;

namespace GameDotNet.Input.Abstract;

public readonly record struct KeyEvent(Key Key);
public readonly record struct MouseClickEvent(MouseButton Button);
public readonly record struct MouseScrollEvent(Vector2 Delta);
public readonly record struct MouseMoveEvent(Vector2 Delta);


public interface IInputContext
{
    public ISubscriber<KeyEvent> KeyDown { get; }
    public ISubscriber<KeyEvent> KeyUp { get; }

    public ISubscriber<MouseClickEvent> MouseClickDown { get; }
    public ISubscriber<MouseClickEvent> MouseClickUp { get; }
    public ISubscriber<MouseScrollEvent> MouseScroll { get; }
    public ISubscriber<MouseMoveEvent> MouseMove { get; }
    
    public bool CursorHidden { get; set; }
    public bool CursorRestricted { get; set; }
    public Vector2 MousePosition { get; }
    
    public bool IsKeyDown(Key key);
    public bool IsButtonDown(MouseButton button);
}