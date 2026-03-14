using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using GameDotNet.Core.Abstractions;
using GameDotNet.Core.Models;
using Microsoft.Extensions.Logging;
using ValueTaskSupplement;
using IInputContext = GameDotNet.Core.Abstractions.IInputContext;
using Key = GameDotNet.Core.Models.Key;
using MouseButton = GameDotNet.Core.Models.MouseButton;
using Size = System.Drawing.Size;

namespace GameDotNet.Graphics.Avalonia;

public abstract class AvaloniaViewPortControl(
    ILogger<AvaloniaViewPortControl> logger,
    IEventBus eventBus
) : Control, IViewPort, IInputContext, IAsyncDisposable
{
    public Size Size => AvaloniaPixelSizeToSize(Bounds.Size);
    public bool IsActive => IsKeyboardFocusWithin;
    public IInputContext Input => this;

    public IAsyncEnumerable<Size> Resized => GetResizedAsync().Select(ev => ev.NewSize);
    public IAsyncEnumerable<bool> FocusAcquired => GetFocusChangedAsync().Select(ev => ev.HasFocus);
    IAsyncEnumerable<Key> IInputContext.KeyDown => GetKeyDownAsync().Select(ev => ev.Key);
    IAsyncEnumerable<Key> IInputContext.KeyUp => GetKeyUpAsync().Select(ev => ev.Key);
    public IAsyncEnumerable<MouseButton> MouseClickDown =>
        GetPointerPressedAsync().Select(ev => ev.Button);
    public IAsyncEnumerable<MouseButton> MouseClickUp =>
        GetPointerReleasedAsync().Select(ev => ev.Button);
    public IAsyncEnumerable<MouseScrollEvent> MouseScroll => GetPointerWheelChangedAsync();
    public IAsyncEnumerable<MouseMoveEvent> MouseMove => GetPointerMovedAsync();

    //TODO: Implement cursor hiding and restriction
    public bool CursorHidden { get; set; }
    public bool CursorRestricted { get; set; }
    public Vector2 MousePosition { get; private set; }
    public bool IsClosing { get; private set; }

    private readonly CancellationTokenSource _cts = new();
    private readonly List<Key> _keysPressed = [];
    private readonly List<MouseButton> _buttonsPressed = [];

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        _cts.Dispose();
    }

    public bool IsKeyDown(Key key)
    {
        return _keysPressed.Contains(key);
    }

    public bool IsButtonDown(MouseButton button)
    {
        return _buttonsPressed.Contains(button);
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();

        Dispatcher.UIThread.InvokeAsync(
            async () => await RegisterEvents(),
            DispatcherPriority.Input,
            _cts.Token
        );
    }

    private async ValueTask RegisterEvents()
    {
        var token = _cts.Token;
        IViewPort viewport = this;

        await ValueTaskEx.WhenAll(
            eventBus.PublishAllAsync(viewport, GetResizedAsync(), token),
            eventBus.PublishAllAsync(viewport, GetFocusChangedAsync(), token),
            eventBus.PublishAllAsync(viewport, GetKeyDownAsync(), token),
            eventBus.PublishAllAsync(viewport, GetKeyUpAsync(), token),
            eventBus.PublishAllAsync(viewport, GetPointerWheelChangedAsync(), token),
            eventBus.PublishAllAsync(viewport, GetPointerMovedAsync(), token),
            eventBus.PublishAllAsync(viewport, GetPointerReleasedAsync(), token),
            eventBus.PublishAllAsync(viewport, GetPointerPressedAsync(), token)
        );
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        _keysPressed.Add(e.Key.ToAbstraction());
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        _keysPressed.Remove(e.Key.ToAbstraction());
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var (pressed, button) = PointerPointToButton(e.GetCurrentPoint(this));
        if (button is not null && pressed)
        {
            _buttonsPressed.Add(button);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        var (pressed, button) = PointerPointToButton(e.GetCurrentPoint(this));

        if (button is not null && !pressed)
        {
            _buttonsPressed.Remove(button);
        }
    }

    private async IAsyncEnumerable<ViewportResizedEvent> GetResizedAsync()
    {
        var sizeChanged = this.GetObservable(SizeChangedEvent).ToAsyncEnumerable();
        await foreach (var args in sizeChanged)
        {
            yield return new(
                this,
                AvaloniaPixelSizeToSize(args.PreviousSize),
                AvaloniaPixelSizeToSize(args.NewSize)
            );
        }
    }

    private async IAsyncEnumerable<ViewportFocusChangedEvent> GetFocusChangedAsync()
    {
        var focusChanged = this.GetObservable(IsKeyboardFocusWithinProperty).ToAsyncEnumerable();
        await foreach (var hasFocus in focusChanged)
        {
            yield return new(this, hasFocus);
        }
    }

    private async IAsyncEnumerable<KeyDownEvent> GetKeyDownAsync()
    {
        var keydown = this.GetObservable(KeyDownEvent).ToAsyncEnumerable();
        await foreach (var args in keydown)
        {
            yield return new(args.Key.ToAbstraction());
        }
    }

    private async IAsyncEnumerable<KeyUpEvent> GetKeyUpAsync()
    {
        var keyup = this.GetObservable(KeyUpEvent).ToAsyncEnumerable();
        await foreach (var args in keyup)
        {
            yield return new(args.Key.ToAbstraction());
        }
    }

    private async IAsyncEnumerable<MouseScrollEvent> GetPointerWheelChangedAsync()
    {
        var pointerWheelChanged = this.GetObservable(PointerWheelChangedEvent).ToAsyncEnumerable();
        await foreach (var args in pointerWheelChanged)
        {
            yield return new(new((float)args.Delta.X, (float)args.Delta.Y));
        }
    }

    private async IAsyncEnumerable<MouseMoveEvent> GetPointerMovedAsync()
    {
        var pointerMoved = this.GetObservable(PointerMovedEvent).ToAsyncEnumerable();
        await foreach (var args in pointerMoved)
        {
            var point = args.GetCurrentPoint(this);
            MousePosition = new((float)point.Position.X, (float)point.Position.Y);
            yield return new(MousePosition);
        }
    }

    private async IAsyncEnumerable<MouseClickUpEvent> GetPointerReleasedAsync()
    {
        var pointerReleased = this.GetObservable(PointerReleasedEvent).ToAsyncEnumerable();
        await foreach (var args in pointerReleased)
        {
            var (pressed, button) = PointerPointToButton(args.GetCurrentPoint(this));
            if (button is not null && !pressed)
            {
                yield return new(button);
            }
        }
    }

    private async IAsyncEnumerable<MouseClickDownEvent> GetPointerPressedAsync()
    {
        var pointerPressed = this.GetObservable(PointerPressedEvent).ToAsyncEnumerable();
        await foreach (var args in pointerPressed)
        {
            var (pressed, button) = PointerPointToButton(args.GetCurrentPoint(this));
            if (button is not null && pressed)
            {
                yield return new(button);
            }
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (e.Root is Window topLevel)
        {
            topLevel.Closing += TopLevelOnClosing;
        }

        //If control has already been sized set size and send event
        if (Bounds != default)
        {
            eventBus.Publish<IViewPort, ViewportResizedEvent>(this, new(this, default, Size));
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (e.Root is Window topLevel)
            topLevel.Closing -= TopLevelOnClosing;

        base.OnDetachedFromVisualTree(e);
    }

    private void TopLevelOnClosing(object? sender, WindowClosingEventArgs e)
    {
        IsClosing = true;
    }

    private (bool pressed, MouseButton? button) PointerPointToButton(PointerPoint point)
    {
        var properties = point.Properties;

        switch (properties.PointerUpdateKind)
        {
            case PointerUpdateKind.LeftButtonPressed:
                return (true, MouseButton.Left);
            case PointerUpdateKind.LeftButtonReleased:
                return (false, MouseButton.Left);
            case PointerUpdateKind.MiddleButtonPressed:
                return (true, MouseButton.Middle);
            case PointerUpdateKind.MiddleButtonReleased:
                return (false, MouseButton.Middle);
            case PointerUpdateKind.RightButtonPressed:
                return (true, MouseButton.Right);
            case PointerUpdateKind.RightButtonReleased:
                return (false, MouseButton.Right);
            case PointerUpdateKind.XButton1Pressed:
            case PointerUpdateKind.XButton2Pressed:
            case PointerUpdateKind.XButton1Released:
            case PointerUpdateKind.XButton2Released:
            case PointerUpdateKind.Other:
                break;
            default:
                logger.LogDebug($"Unhandled PointerUpdateKind: {properties.PointerUpdateKind}");
                break;
        }

        return (false, null);
    }

    private Size AvaloniaPixelSizeToSize(global::Avalonia.Size size)
    {
        var scaling = this.GetVisualRoot()?.RenderScaling ?? 96.0;
        var pxS = PixelSize.FromSize(size, scaling);

        return new(pxS.Width, pxS.Height);
    }
}
