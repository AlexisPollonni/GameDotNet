using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.VisualTree;
using GameDotNet.Core.Tools.Extensions;
using GameDotNet.Graphics.Abstractions;
using MessagePipe;
using Silk.NET.Core.Contexts;
using Silk.NET.SDL;
using Silk.NET.Windowing.Sdl;
using IInputContext = GameDotNet.Input.Abstract.IInputContext;

namespace GameDotNet.Editor.Tools;

internal sealed class AvaloniaNativeView : INativeView, IDisposable
{
    public ISubscriber<System.Drawing.Size> Resized { get; }
    public ISubscriber<bool> FocusChanged { get; }

    public System.Drawing.Size Size { get; private set; }

    public IInputContext Input { get; }
    public bool IsClosing { get; private set; }
    public INativeWindow? Native { get; }


    private readonly double _renderScaling;

    private readonly IDisposablePublisher<System.Drawing.Size> _resized;
    private readonly IDisposablePublisher<bool> _focusChanged;


    public AvaloniaNativeView(Control host, IPlatformHandle handle, EventFactory eventFactory)
    {
        var win = (Avalonia.Controls.Window)host.GetVisualRoot()!;
        _renderScaling = win.RenderScaling;

        (_resized, Resized) = eventFactory.CreateEvent<System.Drawing.Size>();
        (_focusChanged, FocusChanged) = eventFactory.CreateEvent<bool>();

        win.Closing += (_, _) => IsClosing = true;
        host.SizeChanged += (_, args) =>
        {
            Resize(args.NewSize);
        };
        host.GotFocus += (_, _) => _focusChanged.Publish(true);
        host.LostFocus += (_, _) => _focusChanged.Publish(false);

        Native = CreateFromAvaloniaControl(host, handle);
        Input = new AvaloniaInputContext(host, eventFactory);

        //If control has already been sized set size and send event
        if (host.Bounds != default) Resize(host.Bounds.Size);
    }

    private INativeWindow CreateFromAvaloniaControl(Control host, IPlatformHandle parent)
    {
        unsafe
        {
            var sdlView = SdlWindowing.CreateFrom((void*)parent.Handle);

            var handle = SdlWindowing.GetHandle(sdlView);

            var api = SdlWindowing.GetExistingApi(sdlView);

            return new SdlNativeWindow(api, handle);
        }
    }

    private System.Drawing.Size AvaloniaPixelSizeToSize(Size size)
    {
        var pxS = PixelSize.FromSize(size, _renderScaling);

        return new(pxS.Width, pxS.Height);
    }

    private void Resize(Size size)
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