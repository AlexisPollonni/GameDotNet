using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Rendering.Composition;
using Avalonia.VisualTree;

namespace GameDotNet.Editor;

public abstract class DrawingSurfaceDemoBase : Control
{
    private readonly Action _update;
    private CompositionSurfaceVisual? _visual;
    private Compositor? _compositor;
    private string _info;
    private bool _updateQueued;
    private bool _initialized;

    protected CompositionDrawingSurface Surface { get; private set; }

    public DrawingSurfaceDemoBase()
    {
        _update = UpdateFrame;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Initialize();
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        if (_initialized)
            FreeGraphicsResources();
        _initialized = false;
        base.OnDetachedFromLogicalTree(e);
    }

    async void Initialize()
    {
        try
        {
            var selfVisual = ElementComposition.GetElementVisual(this)!;
            _compositor = selfVisual.Compositor;

            Surface = _compositor.CreateDrawingSurface();
            _visual = _compositor.CreateSurfaceVisual();
            _visual.Size = new((float)Bounds.Width, (float)Bounds.Height);
            _visual.Surface = Surface;
            ElementComposition.SetElementChildVisual(this, _visual);
            var (res, info) = await DoInitialize(_compositor, Surface);
            _info = info;
            // if (ParentControl != null)
            //     ParentControl.Info = info;
            _initialized = res;
            QueueNextFrame();
        }
        catch (Exception e)
        {
            // if (ParentControl != null)
            //     ParentControl.Info = e.ToString();
        }
    }

    void UpdateFrame()
    {
        _updateQueued = false;
        var root = this.GetVisualRoot();
        if (root == null)
            return;

        _visual!.Size = new((float)Bounds.Width, (float)Bounds.Height);
        var size = PixelSize.FromSize(Bounds.Size, root.RenderScaling);
        RenderFrame(size);

        QueueNextFrame();
    }

    void QueueNextFrame()
    {
        if (_initialized && !_updateQueued && _compositor != null)
        {
            _updateQueued = true;
            _compositor?.RequestCompositionUpdate(_update);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == BoundsProperty)
            QueueNextFrame();
        base.OnPropertyChanged(change);
    }

    async Task<(bool success, string info)> DoInitialize(Compositor compositor,
                                                         CompositionDrawingSurface compositionDrawingSurface)
    {
        var interop = await compositor.TryGetCompositionGpuInterop();
        if (interop == null)
            return (false, "Compositor doesn't support interop for the current backend");
        return InitializeGraphicsResources(compositor, compositionDrawingSurface, interop);
    }

    protected abstract (bool success, string info) InitializeGraphicsResources(Compositor compositor,
                                                                               CompositionDrawingSurface
                                                                                   compositionDrawingSurface,
                                                                               ICompositionGpuInterop gpuInterop);

    protected abstract void FreeGraphicsResources();


    protected abstract void RenderFrame(PixelSize pixelSize);

    public void Update(ViewPortControl parent)
    {
        ParentControl = parent;
        if (ParentControl != null)
        {
            // ParentControl.Info = _info;
        }

        QueueNextFrame();
    }

    public ViewPortControl? ParentControl { get; private set; }
}