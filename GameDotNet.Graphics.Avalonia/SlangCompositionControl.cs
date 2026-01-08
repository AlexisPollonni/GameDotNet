using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using GameDotNet.Core.Abstractions;
using GameDotNet.Core.Tooling;
using GameDotNet.Graphics.Avalonia.Gpu.Interop;
using GameDotNet.Graphics.Models;
using GameDotNet.Graphics.Services;
using Injectio.Attributes;
using MessagePipe;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Size = System.Drawing.Size;

namespace GameDotNet.Graphics.Avalonia;


/// <summary>
/// Avalonia control that renders using Slang Gfx via composition interop.
/// This provides a surface where you can render your game/3D content.
/// </summary>
[RegisterScoped(Registration = RegistrationStrategy.Self)]
public class SlangCompositionControl(
    ILogger<SlangCompositionControl> logger,
    SlangContext slangContext, 
    ILogger<AvaloniaViewPortControl> viewportLogger, 
    IEventBus eventBus,
    IAsyncRequestHandler<RenderFrameRequest, RenderFramePresentResponse> renderHandler,
    ObjectPool<PooledValueTaskSource> tcsPool) : AvaloniaViewPortControl(viewportLogger, eventBus)
{
    private CancellationTokenSource _initializedCts = new();
    private Task? _renderTask;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        ClipToBounds = true;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _renderTask = InitializeAsync(_initializedCts.Token);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (_renderTask is null)
            return;
        
        _initializedCts.Cancel();

        try
        {
            _renderTask.Wait(2000, _initializedCts.Token);
        }
        catch (OperationCanceledException)
        {
            //swallow expected cancellation
        }
        
        _initializedCts.Dispose();
        _initializedCts = new();
    }

    private async Task InitializeAsync(CancellationToken token)
    {
        var compositor = ElementComposition.GetElementVisual(this)?.Compositor;
        if (compositor == null)
            return;

        var surface = compositor.CreateDrawingSurface();
        var visual = compositor.CreateSurfaceVisual();
        
        visual.Size = new(Size.Width, Size.Height);
        visual.Surface = surface;
        
        ElementComposition.SetElementChildVisual(this, visual);

        var interop = await compositor.TryGetCompositionGpuInterop();
        if (interop == null)
        {
            logger.LogError("Failed to get GPU interop from Avalonia compositor, does your platform support it?");
            return;
        }
        
        var swapchain = new SlangGfxSwapchain(interop, surface, slangContext);

        await RenderLoopAsync(compositor, visual, swapchain, token).ConfigureAwait(false);
    }

    private async Task RenderLoopAsync(Compositor compositor, CompositionSurfaceVisual visual, SlangGfxSwapchain swapchain, CancellationToken token)
    {
        try
        {
            var lastSize = Size;
            while (!token.IsCancellationRequested)
            {
                await Dispatcher.UIThread.AwaitWithPriority(Task.CompletedTask, DispatcherPriority.Render);

                var size = Size;
                if (lastSize != size)
                {
                    visual.Size = new(size.Width, size.Height);
                    lastSize = size;
                }

                await RequestCompositionUpdateAsync(compositor, token).ConfigureAwait(true);
            
                await RenderFrame(size, swapchain, token).ConfigureAwait(false);
            }
        }
        finally
        {
            await swapchain.DisposeAsync().ConfigureAwait(false);
        }
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask RenderFrame(Size size, SlangGfxSwapchain swapchain, CancellationToken token = default)
    {
        if (size == Size.Empty) return;
        
        Dispatcher.UIThread.CheckAccess();
        
        using (swapchain.BeginDraw(new(size.Width, size.Height), out var image))
        {
            await renderHandler.InvokeAsync(new(this, image, new(size.Width, size.Height)), token).ConfigureAwait(true);
        }
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask RequestCompositionUpdateAsync(Compositor compositor, CancellationToken token)
    {
        var tcs = tcsPool.Get();

        try
        {
            //unfortunately lambda capture is unavoidable here
            compositor.RequestCompositionUpdate(() =>
            {
                if (token.IsCancellationRequested)
                    return;
                
                tcs.SetResult();
            });
            
            await tcs.AsValueTask().ConfigureAwait(true);
        }
        finally
        {
            tcsPool.Return(tcs);
        }
    }
}