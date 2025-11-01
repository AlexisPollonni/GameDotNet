using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using GameDotNet.Core.Abstractions;
using MessagePipe;
using IInputContext = GameDotNet.Core.Abstractions.IInputContext;
using Size = System.Drawing.Size;

namespace GameDotNet.Editor.Tools;


internal sealed class AvaloniaViewPort : Control, IViewPort
{
    public ISubscriber<Size> Resized { get; }
    public ISubscriber<bool> FocusAcquired { get; }
    public Size Size { get; }
    public bool IsActive { get; }
    public IInputContext Input { get; }
    

    private readonly double _renderScaling;

    private readonly IDisposablePublisher<Size> _resized;    
    private readonly IDisposablePublisher<bool> _focusChanged;


    public AvaloniaViewPort(EventFactory eventFactory, InputPublisher inputPublisher)
    {
        
        var win = (Window)this.GetVisualRoot()!;

        _renderScaling = win.RenderScaling;

        (_resized, Resized) = eventFactory.CreateEvent<Size>();
        (_focusChanged, FocusAcquired) = eventFactory.CreateEvent<bool>();

        win.Closing += (_, _) => IsClosing = true;
        host.SizeChanged += (_, args) =>
        {
            Resize(args.NewSize);
        };
        host.GotFocus += (_, _) => _focusChanged.Publish(true);
        host.LostFocus += (_, _) => _focusChanged.Publish(false);

        //If control has already been sized set size and send event
        if (host.Bounds != default) Resize(host.Bounds.Size);
    }

    private Size AvaloniaPixelSizeToSize(Avalonia.Size size)
    {
        var pxS = PixelSize.FromSize(size, _renderScaling);

        return new(pxS.Width, pxS.Height);
    }

    private void Resize(Avalonia.Size size)
    {
        var newSize = AvaloniaPixelSizeToSize(size);
        Size = newSize;
        _resized.Publish(newSize);
    }

    public void Dispose()
    {
        Input.DisposeIf();
        _resized.Dispose();
        _focusChanged.Dispose();
    }
}