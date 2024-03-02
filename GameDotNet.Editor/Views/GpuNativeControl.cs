using System;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Platform;
using GameDotNet.Core.Tools.Containers;
using GameDotNet.Core.Tools.Extensions;
using GameDotNet.Editor.Tools;
using GameDotNet.Graphics;
using GameDotNet.Graphics.Abstractions;
using MessagePipe;
using Microsoft.Extensions.DependencyInjection;

namespace GameDotNet.Editor.Views;

public class GpuNativeControl : NativeControlHost
{
    public static readonly StyledProperty<object?> ContentProperty = ContentControl.ContentProperty.AddOwner<GpuNativeControl>();

    public static readonly StyledProperty<IDataTemplate?> ContentTemplateProperty = ContentControl.ContentTemplateProperty.AddOwner<GpuNativeControl>();

    public static readonly StyledProperty<INativeView?> NativeViewProperty = AvaloniaProperty.Register<GpuNativeControl, INativeView?>(
         nameof(NativeView));

    public INativeView? NativeView
    {
        get => GetValue(NativeViewProperty);
        set => SetValue(NativeViewProperty, value);
    }

    [Content]
    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    public IDataTemplate? ContentTemplate
    {
        get => GetValue(ContentTemplateProperty);
        set => SetValue(ContentTemplateProperty, value);
    }

    private Window? _overlayWindow;
    private ICompositeDisposable? _visualDisposables;

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        var handle =  base.CreateNativeControlCore(parent);

        var provider = App.GetServiceProvider();
        var factory =  provider?.GetRequiredService<EventFactory>();
        var viewManager = provider?.GetRequiredService<NativeViewManager>();
        
        var nativeView = new AvaloniaNativeView(_overlayWindow ?? throw new InvalidOperationException(), handle, factory!);
        
        viewManager!.MainView = nativeView;

        NativeView = nativeView;
        
        return handle;
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        var provider = App.GetServiceProvider();
        if (provider is null) return;
        
        var viewManager = provider.GetRequiredService<NativeViewManager>();

        NativeView = null;
        viewManager.MainView?.DisposeIf();
        viewManager.MainView = null;
        
        base.DestroyNativeControlCore(control);
    }

    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);

        _overlayWindow = new()
        {
            SystemDecorations = SystemDecorations.None,
            TransparencyLevelHint = [WindowTransparencyLevel.Transparent],
            Background = Brushes.Transparent,
            SizeToContent = SizeToContent.Manual,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            ZIndex = int.MaxValue,
            Opacity = 1
        };
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);
        
        _overlayWindow?.Close();
        _overlayWindow = null;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _visualDisposables = new DisposableList();
        
        this.GetObservable(BoundsProperty).Subscribe(_ => UpdateOverlayBounds()).DisposeWith(_visualDisposables);
        
        _overlayWindow!.Bind(ContentControl.ContentProperty, this.GetObservable(ContentProperty)).DisposeWith(_visualDisposables);
        _overlayWindow.Bind(ContentControl.ContentTemplateProperty, this.GetObservable(ContentTemplateProperty))
                      .DisposeWith(_visualDisposables);

            
        Observable.FromEventPattern(e.Root, nameof(Window.PositionChanged))
                  .Subscribe(_ => UpdateOverlayBounds())
                  .DisposeWith(_visualDisposables);
            
        _overlayWindow.Show((Window)e.Root);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        
        _visualDisposables?.Dispose();
        _visualDisposables = null;
        
        _overlayWindow?.Hide();
    }

    private void UpdateOverlayBounds()
    {
        if (_overlayWindow is null) return;
        
        var bounds = Bounds;

        _overlayWindow.Width = bounds.Width;
        _overlayWindow.MaxWidth = bounds.Width;
        _overlayWindow.Height = bounds.Height;
        _overlayWindow.MaxHeight = bounds.Height;

        var topLeft = bounds.TopLeft;
        var newPosition = this.PointToScreen(topLeft);

        _overlayWindow.Position = newPosition;
    }
}