using System.Numerics;
using GameDotNet.Input.Abstract;
using MessagePipe;
using Silk.NET.Input;
using IInputContext = GameDotNet.Input.Abstract.IInputContext;


namespace GameDotNet.Input;

public class SilkInputContext : IInputContext
{
    public ISubscriber<KeyEvent> KeyDown { get; }
    public ISubscriber<KeyEvent> KeyUp { get; }
    public ISubscriber<MouseClickEvent> MouseClickDown { get; }
    public ISubscriber<MouseClickEvent> MouseClickUp { get; }
    public ISubscriber<MouseScrollEvent> MouseScroll { get; }
    public ISubscriber<MouseMoveEvent> MouseMove { get; }

    public bool CursorHidden
    {
        get => _ctx.Mice.First().Cursor.CursorMode is CursorMode.Hidden or CursorMode.Disabled or CursorMode.Raw;
        set
        {
            var cur = _ctx.Mice.First().Cursor;
            cur.CursorMode = value ? CursorMode.Hidden : CursorMode.Normal;
        }
    }

    public bool CursorRestricted
    {
        get => _ctx.Mice.First().Cursor.CursorMode is CursorMode.Disabled or CursorMode.Raw;
        set
        {
            var cur = _ctx.Mice.First().Cursor;
            cur.CursorMode = value ? CursorMode.Raw : CursorMode.Normal;
        }
    }

    public Vector2 MousePosition => _ctx.Mice.FirstOrDefault()?.Position ?? Vector2.Zero;


    private readonly Silk.NET.Input.IInputContext _ctx;

    private readonly IDisposablePublisher<KeyEvent> _keyDown;
    private readonly IDisposablePublisher<KeyEvent> _keyUp;
    private readonly IDisposablePublisher<MouseClickEvent> _mouseDown;
    private readonly IDisposablePublisher<MouseClickEvent> _mouseUp;
    private readonly IDisposablePublisher<MouseScrollEvent> _mouseScroll;
    private readonly IDisposablePublisher<MouseMoveEvent> _mouseMove;

    public SilkInputContext(Silk.NET.Input.IInputContext ctx, EventFactory eventFactory)
    {
        _ctx = ctx;

        (_keyDown, KeyDown) = eventFactory.CreateEvent<KeyEvent>();
        (_keyUp, KeyUp) = eventFactory.CreateEvent<KeyEvent>();
        (_mouseDown, MouseClickDown) = eventFactory.CreateEvent<MouseClickEvent>();
        (_mouseUp, MouseClickUp) = eventFactory.CreateEvent<MouseClickEvent>();
        (_mouseScroll, MouseScroll) = eventFactory.CreateEvent<MouseScrollEvent>();
        (_mouseMove, MouseMove) = eventFactory.CreateEvent<MouseMoveEvent>();
        
        foreach (var kb in _ctx.Keyboards) AddDevice(kb);
        foreach (var mouse in _ctx.Mice) AddDevice(mouse);

        _ctx.ConnectionChanged += (device, b) =>
        {
            if (b)
                AddDevice(device);
            else
            {
                //TODO: device removal
            }
        };
    }

    public bool IsKeyDown(Key key) => _ctx.Keyboards.Any(kb => kb.IsKeyPressed(key));
    public bool IsButtonDown(MouseButton button) => _ctx.Mice.Any(mouse => mouse.IsButtonPressed(button));

    private void AddDevice(IInputDevice d)
    {
        switch (d)
        {
            case IKeyboard kb:
                kb.KeyDown += KeyboardOnKeyDown;
                kb.KeyUp += KeyboardOnKeyUp;
                break;
            case Silk.NET.Input.IMouse ms:
                ms.MouseDown += MouseOnMouseDown;
                ms.MouseUp += MouseOnMouseUp;
                ms.Scroll += MouseOnScroll;
                ms.MouseMove += MouseOnMouseMove;
                break;
        }
    }

    private void KeyboardOnKeyUp(IKeyboard kb, Key key, int code)
    {
        _keyUp.Publish(new(key));
    }

    private void KeyboardOnKeyDown(IKeyboard kb, Key key, int code)
    {
        _keyDown.Publish(new(key));
    }

    private void MouseOnMouseDown(Silk.NET.Input.IMouse ms, MouseButton button)
    {
        _mouseDown.Publish(new(button));
    }

    private void MouseOnMouseUp(Silk.NET.Input.IMouse ms, MouseButton button)
    {
        _mouseUp.Publish(new(button));
    }

    private void MouseOnScroll(Silk.NET.Input.IMouse ms, ScrollWheel wheel)
    {
        _mouseScroll.Publish(new(new(wheel.X, wheel.Y)));
    }

    private void MouseOnMouseMove(Silk.NET.Input.IMouse ms, Vector2 delta)
    {
        _mouseMove.Publish(new(delta));
    }
}