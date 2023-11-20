using GameDotNet.Graphics.Abstractions;
using GameDotNet.Input.Abstract;
using MessagePipe;
using Silk.NET.Core.Contexts;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using InputWindowExtensions = Silk.NET.Input.InputWindowExtensions;

namespace GameDotNet.Graphics;

public sealed class SilkView : INativeView, IDisposable
{
    public ISubscriber<Vector2D<int>> Resized { get; }
    public ISubscriber<bool> FocusChanged { get; }
    public Vector2D<int> Size => _view.FramebufferSize;
    public IInputContext Input { get; }
    public bool IsClosing => _view.IsClosing;
    public INativeWindow? Native => _view.Native;

    private readonly IView _view;
    private readonly IDisposablePublisher<Vector2D<int>> _resized;
    private readonly IDisposablePublisher<bool> _focusChanged;

    public SilkView(IView view, EventFactory factory)
    {
        _view = view;
        Input = new SilkInputContext(InputWindowExtensions.CreateInput(view), factory);

        (_resized, Resized) = factory.CreateEvent<Vector2D<int>>();
        (_focusChanged, FocusChanged) = factory.CreateEvent<bool>();
        
        _view.FramebufferResize += s => _resized.Publish(s);
        _view.FocusChanged += b => _focusChanged.Publish(b);
    }

    public void Dispose()
    {
        _resized.Dispose();
        _focusChanged.Dispose();
    }
}