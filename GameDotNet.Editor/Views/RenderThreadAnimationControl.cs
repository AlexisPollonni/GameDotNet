using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Avalonia;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Avalonia.Skia;
using Avalonia.Threading;
using Avalonia.Vulkan;
using GameDotNet.Core.Abstractions;
using GameDotNet.Graphics.Avalonia;
using GameDotNet.Graphics.Models;
using GameDotNet.Graphics.Tooling;
using GameDotNet.Graphics.Vulkan.Abstractions;
using MessagePipe;
using Microsoft.Extensions.Logging;
using Shouldly;
using SkiaSharp;

namespace GameDotNet.Editor.Views;

[RegisterTransient<RenderThreadAnimationControl>]
public class RenderThreadAnimationControl(
    ILogger<RenderThreadAnimationControl> logger,
    IVulkanContext context,
    IAsyncRequestHandler<RenderFrameRequest, RenderFramePresentResponse> renderHandler,
    IVulkanDevice vulkanDevice,
    IEventBus eventBus
) : AvaloniaViewPortControl(logger, eventBus)
{
    private CompositionCustomVisual? _customVisual;
    private CancellationTokenSource _cts = new();
    private Task? _renderTask;
    private CustomVisualHandler? _handler;
    private readonly Channel<SkiaSwapchainImage> _renderedFrameChannel =
        Channel.CreateBounded<SkiaSwapchainImage>(
            new BoundedChannelOptions(2)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = true,
            }
        );
    private readonly Channel<SkiaSwapchainImage> _presentedFrameChannel =
        Channel.CreateBounded<SkiaSwapchainImage>(
            new BoundedChannelOptions(2)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = true,
            }
        );

    public TimelineStats? RenderStats { get; private set; }

    private class CustomVisualHandler(
        ILogger logger,
        Channel<SkiaSwapchainImage> renderChannel,
        Channel<SkiaSwapchainImage> presentedChannel,
        IVulkanDevice device
    ) : CompositionCustomVisualHandler
    {
        private SkiaSwapchainImage? _previousImage;

        public override void OnRender(ImmediateDrawingContext drawingContext)
        {
            if (!renderChannel.Reader.TryRead(out var frame))
            {
                frame = _previousImage;
            }

            var feature = drawingContext.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (feature is null)
                return;

            using var lease = feature.Lease();

            try
            {
                if (frame is null)
                    return;

                using var _ = device.Lock(); // ensure we have access to the Vulkan device for the duration of the draw call

                DrawCanvas(lease.SkCanvas, lease.GrContext.ShouldNotBeNull("No Gr Context"), frame);

                _previousImage = frame;
            }
            catch (Exception e)
            {
                // Log and swallow exceptions from the render path to avoid crashing the UI thread
                // since there's no way to recover from them. The renderer will log the error and attempt
                // to continue rendering future frames.
                logger.LogError(e, "Error rendering frame");
            }
            finally
            {
                if (frame is not null)
                    presentedChannel.Writer.TryWrite(frame);
                RegisterForNextAnimationFrameUpdate();
                Invalidate();
            }
        }

        private void DrawCanvas(SKCanvas canvas, GRContext context, SkiaSwapchainImage image)
        {
            var vkImageInfo = image.ImageInfo with
            {
                CurrentQueueFamily = device.GraphicsQueueFamilyIndex,
            };

            var backendTexture = new GRBackendTexture(
                image.Size.Width,
                image.Size.Height,
                vkImageInfo
            );

            using var skImage = SKImage.FromTexture(
                context,
                backendTexture,
                GRSurfaceOrigin.TopLeft,
                SKColorType.Rgba8888,
                SKAlphaType.Unpremul
            );

            if (skImage is null)
                return;

            var dest = new SKRect(0, 0, (float)EffectiveSize.X, (float)EffectiveSize.Y);
            canvas.DrawImage(skImage, dest);
            context.Flush(true, true);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty && _customVisual is not null)
        {
            //todo move this inside handler for performance
            _customVisual.Compositor.RequestCompositionUpdate(() =>
                _customVisual.Size = new(Bounds.Width, Bounds.Height)
            );
        }
    }

    protected override async void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        try
        {
            base.OnAttachedToVisualTree(e);

            var visual = ElementComposition.GetElementVisual(this);
            if (visual is null)
                return;

            var compositor = visual.Compositor;

            _handler = new(logger, _renderedFrameChannel, _presentedFrameChannel, vulkanDevice);
            _customVisual = compositor.CreateCustomVisual(_handler);
            _customVisual.Size = new(Bounds.Width, Bounds.Height);
            ElementComposition.SetElementChildVisual(this, _customVisual);

            _renderTask = Task.Run(() => RenderLoopAsync(_cts.Token));

            await _renderTask;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error initializing render loop");
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (_renderTask is null)
            return;

        _cts.Cancel();

        try
        {
            _renderTask.Wait(2000);
        }
        catch (OperationCanceledException)
        {
            // expected
        }
        catch (AggregateException ex)
            when (ex.InnerExceptions.All(e2 => e2 is OperationCanceledException))
        {
            // expected
        }

        _customVisual = null;
        _handler = null;

        _cts.Dispose();
        _cts = new();
    }

    private async Task RenderLoopAsync(CancellationToken token)
    {
        //TODO: move this to the frame updater
        try
        {
            // kick off the first frame, otherwise will wait indefinitely until the first frame is presented
            // we use 1 x 1 pixel, creating a 0 size texture is invalid
            while (_presentedFrameChannel.Writer.TryWrite(new(context, new(1, 1))))
                ;

            while (!token.IsCancellationRequested)
            {
                // Render a frame
                await RenderFrame(token).ConfigureAwait(false);
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogError(e, "Error in Skia render loop");
            throw;
        }
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask RenderFrame(CancellationToken token)
    {
        var bounds = Dispatcher.UIThread.Invoke(() => Bounds);
        var pixelSize = new PixelSize((int)bounds.Width, (int)bounds.Height);

        if (pixelSize.Width <= 0 || pixelSize.Height <= 0)
            return;

        var presentImage = await _presentedFrameChannel
            .Reader.ReadAsync(token)
            .ConfigureAwait(false);

        if (presentImage.Size != pixelSize)
        {
            presentImage.Dispose();

            presentImage = new(context, pixelSize);
        }

        // The renderer writes to the VulkanImage (IDeviceTexture) the same as with the interop path.
        var presentResponse = await renderHandler
            .InvokeAsync(new(this, presentImage.Image), token)
            .ConfigureAwait(false);

        Dispatcher.UIThread.Invoke(() => RenderStats = presentResponse.RenderStats);
        await _renderedFrameChannel.Writer.WriteAsync(presentImage, token).ConfigureAwait(false);
    }
}
