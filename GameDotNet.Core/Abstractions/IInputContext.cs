using System.Numerics;
using GameDotNet.Core.Models;

namespace GameDotNet.Core.Abstractions;

public interface IInputContext
{
    public IAsyncEnumerable<Key> KeyDown { get; }
    public IAsyncEnumerable<Key> KeyUp { get; }

    public IAsyncEnumerable<MouseButton> MouseClickDown { get; }
    public IAsyncEnumerable<MouseButton> MouseClickUp { get; }
    public IAsyncEnumerable<MouseScrollEvent> MouseScroll { get; }
    public IAsyncEnumerable<MouseMoveEvent> MouseMove { get; }
    
    public bool CursorHidden { get; set; }
    public bool CursorRestricted { get; set; }
    public Vector2 MousePosition { get; }
    
    public bool IsKeyDown(Key key);
    public bool IsButtonDown(MouseButton button);
}