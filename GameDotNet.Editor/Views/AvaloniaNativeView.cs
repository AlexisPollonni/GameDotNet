using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.VisualTree;
using GameDotNet.Core.Tools.Extensions;
using GameDotNet.Editor.Tools;
using GameDotNet.Graphics.Abstractions;
using MessagePipe;
using Silk.NET.Core.Contexts;
using Silk.NET.Maths;
using IInputContext = GameDotNet.Input.Abstract.IInputContext;

namespace GameDotNet.Editor.Views;

internal sealed class AvaloniaNativeView : INativeView, IDisposable
{
    public ISubscriber<Vector2D<int>> Resized { get; }
    public ISubscriber<bool> FocusChanged { get; }

    public Vector2D<int> Size { get; private set; }

    public IInputContext Input { get; }
    public bool IsClosing { get; private set; }
    public INativeWindow? Native { get; }


    private readonly double _renderScaling;

    private readonly IDisposablePublisher<Vector2D<int>> _resized;
    private readonly IDisposablePublisher<bool> _focusChanged;


    public AvaloniaNativeView(Control host, IPlatformHandle handle, EventFactory eventFactory)
    {
        var win = (Window)host.GetVisualRoot()!;
        _renderScaling = win.RenderScaling;

        (_resized, Resized) = eventFactory.CreateEvent<Vector2D<int>>();
        (_focusChanged, FocusChanged) = eventFactory.CreateEvent<bool>();

        win.Closing += (_, _) => IsClosing = true;
        host.SizeChanged += (_, args) =>
        {
            Resize(args.NewSize);
        };
        host.GotFocus += (_, _) => _focusChanged.Publish(true);
        host.LostFocus += (_, _) => _focusChanged.Publish(false);

        Native = new AvaloniaNativeWindow(handle);
        Input = new AvaloniaInputContext(host, eventFactory);

        //If control has already been sized set size and send event
        if (host.Bounds != default) Resize(host.Bounds.Size);
    }

    private Vector2D<int> AvaloniaPixelSizeToVector(Size size)
    {
        var pxS = PixelSize.FromSize(size, _renderScaling);

        return new(pxS.Width, pxS.Height);
    }

    private void Resize(Size size)
    {
        var newSize = AvaloniaPixelSizeToVector(size);
        Size = newSize;
        _resized.Publish(newSize);
    }

    private class AvaloniaNativeWindow : INativeWindow
    {
        public AvaloniaNativeWindow(IPlatformHandle handle)
        {
            if (OperatingSystem.IsWindows())
            {
                Kind = NativeWindowFlags.Win32;
                Win32 = new ValueTuple<nint, nint, nint>(handle.Handle, IntPtr.Zero, WinApi.GetModuleHandle(null));
            }
        }

        public NativeWindowFlags Kind { get; }
        public (nint Display, nuint Window)? X11 { get; }
        public nint? Cocoa { get; }
        public (nint Display, nint Surface)? Wayland { get; }
        public nint? WinRT { get; }
        public (nint Window, uint Framebuffer, uint Colorbuffer, uint ResolveFramebuffer)? UIKit { get; }
        public (nint Hwnd, nint HDC, nint HInstance)? Win32 { get; }
        public (nint Display, nint Window)? Vivante { get; }
        public (nint Window, nint Surface)? Android { get; }
        public nint? Glfw { get; }
        public nint? Sdl { get; }
        public nint? DXHandle { get; }
        public (nint? Display, nint? Surface)? EGL { get; }
    }

    public void Dispose()
    {
        Input.DisposeIf();
        _resized.Dispose();
        _focusChanged.Dispose();
    }
}