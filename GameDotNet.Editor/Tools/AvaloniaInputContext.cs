using System.Numerics;
using Avalonia.Input;
using GameDotNet.Core.Abstractions;
using Key = GameDotNet.Core.Abstractions.Key;
using MouseButton = GameDotNet.Core.Abstractions.MouseButton;

namespace GameDotNet.Editor.Tools;

internal sealed class AvaloniaInputContext : IInputContext, IDisposable
{
    private readonly InputPublisher _inputPublisher;

    public bool CursorHidden { get; set; }

    public bool CursorRestricted { get; set; }

    public Vector2 MousePosition { get; private set; }
    private readonly AvaloniaViewPort _viewport;
    private readonly List<Key> _keysPressed;
    private readonly List<MouseButton> _buttonPressed;

    public AvaloniaInputContext(AvaloniaViewPort viewport, InputPublisher inputPublisher)
    {
        _inputPublisher = inputPublisher;
        
        _viewport = viewport;
        _keysPressed = [];
        _buttonPressed = [];
        
        viewport.KeyDown += ElementOnKeyDown;
        viewport.KeyUp += ElementOnKeyUp;
        viewport.PointerPressed += ElementOnPointerPressed;
        viewport.PointerReleased += ElementOnPointerReleased;
        viewport.PointerWheelChanged += ElementOnPointerWheelChanged;
        viewport.PointerMoved += ElementOnPointerMoved;
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
                _inputPublisher.SendMouseClickDown(_viewport, kind.GetMouseButton().ToAbstraction());
            }
            else
            {
                _inputPublisher.SendMouseClickUp(_viewport, kind.GetMouseButton().ToAbstraction());
            }

            return;
        }

        var point = e.GetPosition(_viewport);
        var vecPos = new Vector2((float)point.X, (float)point.Y);

        var delta = vecPos - MousePosition;

        _inputPublisher.SendMouseMove(_viewport, delta);
        MousePosition = vecPos;
    }

    private void ElementOnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        var delta = new Vector2((float)e.Delta.X, (float)e.Delta.Y);
        _inputPublisher.SendMouseScroll(_viewport,delta);
    }

    private void ElementOnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var button = e.GetCurrentPoint(null).Properties.PointerUpdateKind.GetMouseButton().ToAbstraction();
        _buttonPressed.Remove(button);
        _inputPublisher.SendMouseClickUp(_viewport, button);
    }

    private void ElementOnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var button = e.GetCurrentPoint(null).Properties.PointerUpdateKind.GetMouseButton().ToAbstraction();
        _buttonPressed.Add(button);
        _inputPublisher.SendMouseClickDown(_viewport, button);
    }

    private void ElementOnKeyDown(object? sender, KeyEventArgs e)
    {
        var key = e.Key.ToSilkKey();
        _keysPressed.Add(key);
        _inputPublisher.SendKeyDown(_viewport, key);
    }

    private void ElementOnKeyUp(object? sender, KeyEventArgs e)
    {
        var key = e.Key.ToSilkKey();
        _keysPressed.Remove(key);
        _inputPublisher.SendKeyUp(_viewport, key);
    }

    public bool IsKeyDown(Key key) => _keysPressed.Contains(key);

    public bool IsButtonDown(MouseButton button) => _buttonPressed.Contains(button);
}