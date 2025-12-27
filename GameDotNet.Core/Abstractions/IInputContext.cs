using System.Numerics;
using GameDotNet.Core.Models;
using MessagePipe;

namespace GameDotNet.Core.Abstractions;

public interface IInputContext
{
    public ISubscriber<Key> KeyDown { get; }
    public ISubscriber<Key> KeyUp { get; }

    public ISubscriber<MouseButton> MouseClickDown { get; }
    public ISubscriber<MouseButton> MouseClickUp { get; }
    public ISubscriber<MouseScrollEvent> MouseScroll { get; }
    public ISubscriber<MouseMoveEvent> MouseMove { get; }
    
    public bool CursorHidden { get; set; }
    public bool CursorRestricted { get; set; }
    public Vector2 MousePosition { get; }
    
    public bool IsKeyDown(Key key);
    public bool IsButtonDown(MouseButton button);
}