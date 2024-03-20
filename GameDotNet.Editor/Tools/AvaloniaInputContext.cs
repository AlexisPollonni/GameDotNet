using System;
using System.Collections.Generic;
using System.Numerics;
using Avalonia;
using Avalonia.Input;
using GameDotNet.Input.Abstract;
using MessagePipe;
using Key = Silk.NET.Input.Key;
using MouseButton = Silk.NET.Input.MouseButton;

namespace GameDotNet.Editor.Tools;

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
        MousePosition = vecPos;
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