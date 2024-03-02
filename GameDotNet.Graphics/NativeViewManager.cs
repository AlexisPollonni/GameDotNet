using GameDotNet.Graphics.Abstractions;
using MessagePipe;

namespace GameDotNet.Graphics;

public sealed class NativeViewManager : IDisposable
{
    public INativeView? MainView
    {
        get => _mainView;
        set
        {
            _mainView = value;
            _viewChanged.Publish(value);
        }
    }

    public ISubscriber<INativeView?> MainViewChanged { get; set; }
    
    
    private INativeView? _mainView;
    private readonly IDisposablePublisher<INativeView?> _viewChanged;

    public NativeViewManager(EventFactory factory)
    {
        (_viewChanged, MainViewChanged) = factory.CreateEvent<INativeView?>();
    }

    public void Dispose()
    {
        _viewChanged.Dispose();
    }
}