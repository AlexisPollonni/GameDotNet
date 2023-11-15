using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using GameDotNet.Editor.Tools;
using GameDotNet.Graphics.Abstractions;
using Silk.NET.Core.Contexts;
using Silk.NET.Maths;

namespace GameDotNet.Editor.Views;

internal class AvaloniaNativeView : INativeView
{
    private readonly NativeControlHost _host;
    private readonly double _renderScaling;

    public event Action<Vector2D<int>>? Resized;

    public Vector2D<int> Size
    {
        get
        {
            return Dispatcher.UIThread.Invoke(() => AvaloniaBoundsRectToVector(_host.Bounds));
        }
    }

    public bool IsClosing { get; private set; }

    public INativeWindow? Native { get; }

    public AvaloniaNativeView(NativeControlHost host, IPlatformHandle handle)
    {
        _host = host;

        var win = _host.GetVisualRoot() as Window;
        _renderScaling = win?.RenderScaling ?? 1;

        win.Closing += (_, _) => IsClosing = true;
        
        host.SizeChanged += (_, args) =>
        {
            Resized?.Invoke(AvaloniaPixelSizeToVector(args.NewSize));
        };

        Native = new AvaloniaNativeWindow(handle);
    }

    private Vector2D<int> AvaloniaBoundsRectToVector(Rect r) => AvaloniaPixelSizeToVector(r.Size);

    private Vector2D<int> AvaloniaPixelSizeToVector(Size size)
    {
        var pxS = PixelSize.FromSize(size, _renderScaling);

        return new(pxS.Width, pxS.Height);
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
}