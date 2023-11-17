using Silk.NET.Core.Contexts;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace GameDotNet.Graphics.Abstractions;

public interface INativeView
{
    event Action<Vector2D<int>>? Resized;

    public Vector2D<int> Size { get; }
    
    public bool IsClosing { get; }
    
    public INativeWindow? Native { get; }
}

public class SilkView : INativeView
{
    private readonly IView _view;

    public SilkView(IView view)
    {
        _view = view;

        _view.FramebufferResize += s => Resized?.Invoke(s);
    }

    public event Action<Vector2D<int>>? Resized;
    public Vector2D<int> Size => _view.FramebufferSize;
    public bool IsClosing => _view.IsClosing;
    public INativeWindow? Native => _view.Native;
}
