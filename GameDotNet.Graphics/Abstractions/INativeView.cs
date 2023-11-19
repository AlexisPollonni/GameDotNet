using MessagePipe;
using Silk.NET.Core.Contexts;
using Silk.NET.Maths;
using IInputContext = GameDotNet.Input.Abstract.IInputContext;

namespace GameDotNet.Graphics.Abstractions;

public interface INativeView
{
    public ISubscriber<Vector2D<int>> Resized { get; }
    public ISubscriber<bool> FocusChanged { get; }

    public Vector2D<int> Size { get; }
    
    public IInputContext Input { get; }
    
    public bool IsClosing { get; }
    
    public INativeWindow? Native { get; }
}