using System;
using System.Collections.Generic;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform;
using Avalonia.VisualTree;
using GameDotNet.Core.Tools.Extensions;
using GameDotNet.Editor.Tools;
using GameDotNet.Graphics.Abstractions;
using GameDotNet.Input.Abstract;
using MessagePipe;
using Silk.NET.Core.Contexts;
using Silk.NET.Maths;
using IInputContext = GameDotNet.Input.Abstract.IInputContext;
using Key = Silk.NET.Input.Key;
using MouseButton = Silk.NET.Input.MouseButton;

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

internal sealed class AvaloniaInputContext : IInputContext, IDisposable
{
    public ISubscriber<KeyEvent> KeyDown { get; }
    public ISubscriber<KeyEvent> KeyUp { get; }

    public ISubscriber<MouseClickEvent> MouseClickDown { get; }

    public ISubscriber<MouseClickEvent> MouseClickUp { get; }

    public ISubscriber<MouseScrollEvent> MouseScroll { get; }

    public ISubscriber<MouseMoveEvent> MouseMove { get; }

    public bool CursorHidden { get; set; }

    public bool CursorRestricted { get; set; }

    public Vector2 MousePosition { get; private set; }
    private readonly IInputElement _element;
    private readonly List<Key> _keysPressed;
    private readonly List<MouseButton> _buttonPressed;
    private readonly IDisposablePublisher<KeyEvent> _keyDown;
    private readonly IDisposablePublisher<KeyEvent> _keyUp;
    private readonly IDisposablePublisher<MouseClickEvent> _mouseDown;
    private readonly IDisposablePublisher<MouseClickEvent> _mouseUp;
    private readonly IDisposablePublisher<MouseScrollEvent> _mouseScroll;
    private readonly IDisposablePublisher<MouseMoveEvent> _mouseMove;

    public AvaloniaInputContext(IInputElement element, EventFactory eventFactory)
    {
        _element = element;
        _keysPressed = new();
        _buttonPressed = new();
        
        (_keyDown, KeyDown) = eventFactory.CreateEvent<KeyEvent>();
        (_keyUp, KeyUp) = eventFactory.CreateEvent<KeyEvent>();
        (_mouseDown, MouseClickDown) = eventFactory.CreateEvent<MouseClickEvent>();
        (_mouseUp, MouseClickUp) = eventFactory.CreateEvent<MouseClickEvent>();
        (_mouseScroll, MouseScroll) = eventFactory.CreateEvent<MouseScrollEvent>();
        (_mouseMove, MouseMove) = eventFactory.CreateEvent<MouseMoveEvent>();
        
        element.KeyDown += ElementOnKeyDown;
        element.KeyUp += ElementOnKeyUp;
        element.PointerPressed += ElementOnPointerPressed;
        element.PointerReleased += ElementOnPointerReleased;
        element.PointerWheelChanged += ElementOnPointerWheelChanged;
        element.PointerMoved += ElementOnPointerMoved;
    }

    private void ElementOnPointerMoved(object? sender, PointerEventArgs e)
    {
        var kind = e.GetCurrentPoint(null).Properties.PointerUpdateKind;
        if(kind is not PointerUpdateKind.Other) //https://docs.microsoft.com/en-us/uwp/api/windows.ui.xaml.uielement.pointerreleased
        {
            if (kind is PointerUpdateKind.LeftButtonPressed 
                or PointerUpdateKind.MiddleButtonPressed 
                or PointerUpdateKind.RightButtonPressed
                or PointerUpdateKind.XButton1Pressed
                or PointerUpdateKind.XButton2Pressed)
            {
                _mouseDown.Publish(new(kind.GetMouseButton().ToSilkButton()));
            }
            else
            {
                _mouseUp.Publish(new(kind.GetMouseButton().ToSilkButton()));
            }

            return;
        }

        var point = e.GetPosition(_element as Visual);
        var vecPos = new Vector2((float)point.X, (float)point.Y);

        var delta = vecPos - MousePosition;
        
        _mouseMove.Publish(new(delta));
    }

    private void ElementOnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        var delta = new Vector2((float)e.Delta.X, (float)e.Delta.Y);
        _mouseScroll.Publish(new(delta));
    }

    private void ElementOnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var button = e.GetCurrentPoint(null).Properties.PointerUpdateKind.GetMouseButton().ToSilkButton();
        _buttonPressed.Remove(button);
        _mouseUp.Publish(new(button));
    }

    private void ElementOnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var button = e.GetCurrentPoint(null).Properties.PointerUpdateKind.GetMouseButton().ToSilkButton();
        _buttonPressed.Add(button);
        _mouseDown.Publish(new(button));
    }

    private void ElementOnKeyDown(object? sender, KeyEventArgs e)
    {
        var key = e.Key.ToSilkKey();
        _keysPressed.Add(key);
        _keyDown.Publish(new(key));
    }

    private void ElementOnKeyUp(object? sender, KeyEventArgs e)
    {
        var key = e.Key.ToSilkKey();
        _keysPressed.Remove(key);
        _keyUp.Publish(new(key));
    }

    public bool IsKeyDown(Key key) => _keysPressed.Contains(key);

    public bool IsButtonDown(MouseButton button) => _buttonPressed.Contains(button);

    public void Dispose()
    {
        _keyDown.Dispose();
        _keyUp.Dispose();
        _mouseDown.Dispose();
        _mouseUp.Dispose();
        _mouseScroll.Dispose();
        _mouseMove.Dispose();
    }
}

public static class AvaloniaExtensions
{
    public static MouseButton ToSilkButton(this Avalonia.Input.MouseButton button) => button switch
    {
        Avalonia.Input.MouseButton.None => MouseButton.Unknown,
        Avalonia.Input.MouseButton.Left => MouseButton.Left,
        Avalonia.Input.MouseButton.Right => MouseButton.Right,
        Avalonia.Input.MouseButton.Middle => MouseButton.Middle,
        Avalonia.Input.MouseButton.XButton1 => MouseButton.Button4,
        Avalonia.Input.MouseButton.XButton2 => MouseButton.Button5,
        _ => MouseButton.Unknown
    };
    
    public static Key ToSilkKey(this Avalonia.Input.Key key) => key switch
    {
        Avalonia.Input.Key.None => Key.Unknown,
        Avalonia.Input.Key.Back => Key.Backspace,
        Avalonia.Input.Key.Tab => Key.Tab,
        Avalonia.Input.Key.Return => Key.Enter,
        Avalonia.Input.Key.Pause => Key.Pause,
        Avalonia.Input.Key.CapsLock => Key.CapsLock,
        Avalonia.Input.Key.Escape => Key.Escape,
        Avalonia.Input.Key.Space => Key.Space,
        Avalonia.Input.Key.PageUp => Key.PageUp,
        Avalonia.Input.Key.PageDown => Key.PageDown,
        Avalonia.Input.Key.End => Key.End,
        Avalonia.Input.Key.Home => Key.Home,
        Avalonia.Input.Key.Left => Key.Left,
        Avalonia.Input.Key.Up => Key.Up,
        Avalonia.Input.Key.Right => Key.Right,
        Avalonia.Input.Key.Down => Key.Down,
        Avalonia.Input.Key.Print => Key.PrintScreen,
        Avalonia.Input.Key.Insert => Key.Insert,
        Avalonia.Input.Key.Delete => Key.Delete,
        Avalonia.Input.Key.D0 => Key.Number0,
        Avalonia.Input.Key.D1 => Key.Number1,
        Avalonia.Input.Key.D2 => Key.Number2,
        Avalonia.Input.Key.D3 => Key.Number3,
        Avalonia.Input.Key.D4 => Key.Number4,
        Avalonia.Input.Key.D5 => Key.Number5,
        Avalonia.Input.Key.D6 => Key.Number6,
        Avalonia.Input.Key.D7 => Key.Number7,
        Avalonia.Input.Key.D8 => Key.Number8,
        Avalonia.Input.Key.D9 => Key.Number9,
        Avalonia.Input.Key.A => Key.A,
        Avalonia.Input.Key.B => Key.B,
        Avalonia.Input.Key.C => Key.C,
        Avalonia.Input.Key.D => Key.D,
        Avalonia.Input.Key.E => Key.E,
        Avalonia.Input.Key.F => Key.F,
        Avalonia.Input.Key.G => Key.G,
        Avalonia.Input.Key.H => Key.H,
        Avalonia.Input.Key.I => Key.I,
        Avalonia.Input.Key.J => Key.J,
        Avalonia.Input.Key.K => Key.K,
        Avalonia.Input.Key.L => Key.L,
        Avalonia.Input.Key.M => Key.M,
        Avalonia.Input.Key.N => Key.N,
        Avalonia.Input.Key.O => Key.O,
        Avalonia.Input.Key.P => Key.P,
        Avalonia.Input.Key.Q => Key.Q,
        Avalonia.Input.Key.R => Key.R,
        Avalonia.Input.Key.S => Key.S,
        Avalonia.Input.Key.T => Key.T,
        Avalonia.Input.Key.U => Key.U,
        Avalonia.Input.Key.V => Key.V,
        Avalonia.Input.Key.W => Key.W,
        Avalonia.Input.Key.X => Key.X,
        Avalonia.Input.Key.Y => Key.Y,
        Avalonia.Input.Key.Z => Key.Z,
        Avalonia.Input.Key.LWin => Key.SuperLeft,
        Avalonia.Input.Key.RWin => Key.SuperRight,
        Avalonia.Input.Key.Sleep => Key.Pause,
        Avalonia.Input.Key.NumPad0 => Key.Keypad0,
        Avalonia.Input.Key.NumPad1 => Key.Keypad1,
        Avalonia.Input.Key.NumPad2 => Key.Keypad2,
        Avalonia.Input.Key.NumPad3 => Key.Keypad3,
        Avalonia.Input.Key.NumPad4 => Key.Keypad4,
        Avalonia.Input.Key.NumPad5 => Key.Keypad5,
        Avalonia.Input.Key.NumPad6 => Key.Keypad6,
        Avalonia.Input.Key.NumPad7 => Key.Keypad7,
        Avalonia.Input.Key.NumPad8 => Key.Keypad8,
        Avalonia.Input.Key.NumPad9 => Key.Keypad9,
        Avalonia.Input.Key.Multiply => Key.KeypadMultiply,
        Avalonia.Input.Key.Add => Key.KeypadAdd,
        Avalonia.Input.Key.Subtract => Key.KeypadSubtract,
        Avalonia.Input.Key.Decimal => Key.KeypadDecimal,
        Avalonia.Input.Key.Divide => Key.KeypadDivide,
        Avalonia.Input.Key.F1 => Key.F1,
        Avalonia.Input.Key.F2 => Key.F2,
        Avalonia.Input.Key.F3 => Key.F3,
        Avalonia.Input.Key.F4 => Key.F4,
        Avalonia.Input.Key.F5 => Key.F5,
        Avalonia.Input.Key.F6 => Key.F6,
        Avalonia.Input.Key.F7 => Key.F7,
        Avalonia.Input.Key.F8 => Key.F8,
        Avalonia.Input.Key.F9 => Key.F9,
        Avalonia.Input.Key.F10 => Key.F10,
        Avalonia.Input.Key.F11 => Key.F11,
        Avalonia.Input.Key.F12 => Key.F12,
        Avalonia.Input.Key.F13 => Key.F13,
        Avalonia.Input.Key.F14 => Key.F14,
        Avalonia.Input.Key.F15 => Key.F15,
        Avalonia.Input.Key.F16 => Key.F16,
        Avalonia.Input.Key.F17 => Key.F17,
        Avalonia.Input.Key.F18 => Key.F18,
        Avalonia.Input.Key.F19 => Key.F19,
        Avalonia.Input.Key.F20 => Key.F20,
        Avalonia.Input.Key.F21 => Key.F21,
        Avalonia.Input.Key.F22 => Key.F22,
        Avalonia.Input.Key.F23 => Key.F23,
        Avalonia.Input.Key.F24 => Key.F24,
        Avalonia.Input.Key.NumLock => Key.NumLock,
        Avalonia.Input.Key.Scroll => Key.ScrollLock,
        Avalonia.Input.Key.LeftShift => Key.ShiftLeft,
        Avalonia.Input.Key.RightShift => Key.ShiftRight,
        Avalonia.Input.Key.LeftCtrl => Key.ControlLeft,
        Avalonia.Input.Key.RightCtrl => Key.ControlRight,
        Avalonia.Input.Key.LeftAlt => Key.AltLeft,
        Avalonia.Input.Key.RightAlt => Key.AltRight,
        Avalonia.Input.Key.OemSemicolon => Key.Semicolon,
        Avalonia.Input.Key.OemComma => Key.Comma,
        Avalonia.Input.Key.OemMinus => Key.Minus,
        Avalonia.Input.Key.OemPeriod => Key.Period,
        Avalonia.Input.Key.OemOpenBrackets => Key.LeftBracket,
        Avalonia.Input.Key.OemCloseBrackets => Key.RightBracket,
        Avalonia.Input.Key.OemBackslash => Key.BackSlash,
        _ => Key.Unknown
    };
}