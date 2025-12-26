using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using GameDotNet.Core.Abstractions;
using GameDotNet.Core.Tooling.Extensions;
using MessagePipe;
using IInputContext = GameDotNet.Core.Abstractions.IInputContext;
using Key = GameDotNet.Core.Abstractions.Key;
using MouseButton = GameDotNet.Core.Abstractions.MouseButton;
using Size = System.Drawing.Size;

namespace GameDotNet.Editor.Tools;


internal sealed class AvaloniaViewPort(
    //Viewport subscribers
    IScopedSubscriber<ViewportResizedEvent> resized,
    IScopedSubscriber<ViewportFocusChangedEvent> focusChanged,

    //Viewport publishers
    IScopedPublisher<ViewportResizedEvent> resizePublisher,
    IScopedPublisher<ViewportFocusChangedEvent> focusPublisher,

    //Input subscribers
    IScopedSubscriber<KeyDownEvent> keyDown,
    IScopedSubscriber<KeyUpEvent> keyUp,
    IScopedSubscriber<MouseClickDownEvent> mouseClickDown,
    IScopedSubscriber<MouseClickUpEvent> mouseClickUp,
    IScopedSubscriber<MouseScrollEvent> mouseScroll,
    IScopedSubscriber<MouseMoveEvent> mouseMove,

    ISingletonSubscriber<IViewPort, KeyDownEvent> keyDownS,
    //Input publishers
    EngineEventPublisher inputPublisher
    ) : Control, IViewPort, IInputContext
{
    public IScopedSubscriber<ViewportResizedEvent> Resized { get; } = resized;
    public IScopedSubscriber<ViewportFocusChangedEvent> FocusAcquired { get; } = focusChanged;


    IScopedSubscriber<KeyDownEvent> IInputContext.KeyDown { get; } = keyDown;
    IScopedSubscriber<KeyUpEvent> IInputContext.KeyUp { get; } = keyUp;
    public IScopedSubscriber<MouseClickDownEvent> MouseClickDown { get; } = mouseClickDown;
    public IScopedSubscriber<MouseClickUpEvent> MouseClickUp { get; } = mouseClickUp;
    public IScopedSubscriber<MouseScrollEvent> MouseScroll { get; } = mouseScroll;
    public IScopedSubscriber<MouseMoveEvent> MouseMove { get; } = mouseMove;


    public Size Size { get; private set; }
    public bool IsActive => IsFocused;
    public IInputContext Input => this;
    
    public bool CursorHidden { get; set; }
    public bool CursorRestricted { get; set; }
    public Vector2 MousePosition { get; private set; }
    
    public bool IsClosing { get; private set; }


    public void Dispose()
    {keyDownS.
        Input.DisposeIf();
    }

    public bool IsKeyDown(Key key)
    {
        throw new NotImplementedException();
    }

    public bool IsButtonDown(MouseButton button)
    {
        throw new NotImplementedException();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (e.Root is Window topLevel)
        {
            topLevel.Closing += TopLevelOnClosing;
        }
        
        //If control has already been sized set size and send event
        if (Bounds != default) Resize(Bounds.Size);    
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (e.Root is Window topLevel) topLevel.Closing -= TopLevelOnClosing;
            
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        
        Resize(e.NewSize);
    }

    protected override void OnGotFocus(GotFocusEventArgs e)
    {
        base.OnGotFocus(e);
        
        focusPublisher.Publish(new(this, true));
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        
        focusPublisher.Publish(new(this, false));
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        
        var key = e.Key.ToAbstraction();
        _keysPressed.Add(key);
        _inputPublisher.SendKeyDown(_viewport, key);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
    }


    private void TopLevelOnClosing(object? sender, WindowClosingEventArgs e)
    {
        IsClosing = true;
    }

    private Size AvaloniaPixelSizeToSize(Avalonia.Size size)
    {
        var scaling = this.GetVisualRoot()?.RenderScaling ?? 96.0;
        var pxS = PixelSize.FromSize(size, scaling);

        return new(pxS.Width, pxS.Height);
    }

    private void Resize(Avalonia.Size size)
    {
        var newSize = AvaloniaPixelSizeToSize(size);
        resizePublisher.Publish(new(this, Size, newSize));
        Size = newSize;
    }
}