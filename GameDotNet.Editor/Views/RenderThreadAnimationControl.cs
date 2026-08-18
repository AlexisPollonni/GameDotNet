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
using Silk.NET.Vulkan;
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
            else if (
                _previousImage is not null
                && !presentedChannel.Writer.TryWrite(_previousImage)
            )
                _previousImage.Dispose(); //if a frame was presented and the channel completed at the same time, dispose the frame so it is not leaked

            var feature = drawingContext.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (feature is null)
                return;

            try
            {
                using var lease = feature.Lease();
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
                RegisterForNextAnimationFrameUpdate();
                Invalidate();
            }
        }

        private void DrawCanvas(SKCanvas canvas, GRContext context, SkiaSwapchainImage image)
        {
            var skFormat = image.Image.Format.ToSkiaColorType();

            var vkImageInfo = image.ImageInfo with
            {
                CurrentQueueFamily = device.GraphicsQueueFamilyIndex,
            };

            var backendTexture = new GRBackendTexture(
                image.Size.Width,
                image.Size.Height,
                vkImageInfo
            );

            // Hardening to avoid FromTexture to SegFault the app if the image was disposed. Should never throw.
            if (image.IsDisposeStarted || image.Image.IsDisposeStarted)
            {
                throw new ObjectDisposedException(
                    $"{image} was disposed before it could be rendered by skia"
                );
            }
            using var skImage = SKImage.FromTexture(
                context,
                backendTexture,
                GRSurfaceOrigin.TopLeft,
                skFormat,
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

        // we drain the channels and dispose the remaining hanging frames
        while (_presentedFrameChannel.Reader.TryRead(out var presentFrame))
        {
            presentFrame.Dispose();
        }

        while (_renderedFrameChannel.Reader.TryRead(out var renderedFrame))
        {
            renderedFrame.Dispose();
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

    protected override async ValueTask DisposeAsyncCore()
    {
        _renderedFrameChannel.Writer.Complete();
        _presentedFrameChannel.Writer.Complete();

        await _cts.CancelAsync();
        _cts.Dispose();

        await foreach (var frame in _renderedFrameChannel.Reader.ReadAllAsync())
        {
            frame.Dispose();
        }

        await foreach (var frame in _presentedFrameChannel.Reader.ReadAllAsync())
        {
            frame.Dispose();
        }

        await base.DisposeAsyncCore();
    }
}

internal static class SkiaExtensions
{
    extension(Format vkFormat)
    {
        public SKColorType ToSkiaColorType() =>
            vkFormat switch
            {
                Format.Undefined => SKColorType.Unknown,
                Format.R4G4UnormPack8 => SKColorType.Unknown,
                Format.R4G4B4A4UnormPack16 => SKColorType.Argb4444,
                Format.B4G4R4A4UnormPack16 => SKColorType.Argb4444,
                Format.R5G6B5UnormPack16 => SKColorType.Rgb565,
                Format.B5G6R5UnormPack16 => SKColorType.Unknown,
                Format.R5G5B5A1UnormPack16 => SKColorType.Unknown,
                Format.B5G5R5A1UnormPack16 => SKColorType.Unknown,
                Format.A1R5G5B5UnormPack16 => SKColorType.Unknown,
                Format.R8Unorm => SKColorType.R8Unorm,
                Format.R8SNorm => SKColorType.Unknown,
                Format.R8Uscaled => SKColorType.Unknown,
                Format.R8Sscaled => SKColorType.Unknown,
                Format.R8Uint => SKColorType.Unknown,
                Format.R8Sint => SKColorType.Unknown,
                Format.R8Srgb => SKColorType.Unknown,
                Format.R8G8Unorm => SKColorType.Rg88,
                Format.R8G8SNorm => SKColorType.Unknown,
                Format.R8G8Uscaled => SKColorType.Unknown,
                Format.R8G8Sscaled => SKColorType.Unknown,
                Format.R8G8Uint => SKColorType.Unknown,
                Format.R8G8Sint => SKColorType.Unknown,
                Format.R8G8Srgb => SKColorType.Unknown,
                Format.R8G8B8Unorm => SKColorType.Unknown,
                Format.R8G8B8SNorm => SKColorType.Unknown,
                Format.R8G8B8Uscaled => SKColorType.Unknown,
                Format.R8G8B8Sscaled => SKColorType.Unknown,
                Format.R8G8B8Uint => SKColorType.Unknown,
                Format.R8G8B8Sint => SKColorType.Unknown,
                Format.R8G8B8Srgb => SKColorType.Unknown,
                Format.B8G8R8Unorm => SKColorType.Unknown,
                Format.B8G8R8SNorm => SKColorType.Unknown,
                Format.B8G8R8Uscaled => SKColorType.Unknown,
                Format.B8G8R8Sscaled => SKColorType.Unknown,
                Format.B8G8R8Uint => SKColorType.Unknown,
                Format.B8G8R8Sint => SKColorType.Unknown,
                Format.B8G8R8Srgb => SKColorType.Unknown,
                Format.R8G8B8A8Unorm => SKColorType.Rgba8888,
                Format.R8G8B8A8SNorm => SKColorType.Unknown,
                Format.R8G8B8A8Uscaled => SKColorType.Unknown,
                Format.R8G8B8A8Sscaled => SKColorType.Unknown,
                Format.R8G8B8A8Uint => SKColorType.Unknown,
                Format.R8G8B8A8Sint => SKColorType.Unknown,
                Format.R8G8B8A8Srgb => SKColorType.Srgba8888,
                Format.B8G8R8A8Unorm => SKColorType.Bgra8888,
                Format.B8G8R8A8SNorm => SKColorType.Unknown,
                Format.B8G8R8A8Uscaled => SKColorType.Unknown,
                Format.B8G8R8A8Sscaled => SKColorType.Unknown,
                Format.B8G8R8A8Uint => SKColorType.Unknown,
                Format.B8G8R8A8Sint => SKColorType.Unknown,
                Format.B8G8R8A8Srgb => SKColorType.Unknown,
                Format.A8B8G8R8UnormPack32 => SKColorType.Unknown,
                Format.A8B8G8R8SNormPack32 => SKColorType.Unknown,
                Format.A8B8G8R8UscaledPack32 => SKColorType.Unknown,
                Format.A8B8G8R8SscaledPack32 => SKColorType.Unknown,
                Format.A8B8G8R8UintPack32 => SKColorType.Unknown,
                Format.A8B8G8R8SintPack32 => SKColorType.Unknown,
                Format.A8B8G8R8SrgbPack32 => SKColorType.Unknown,
                Format.A2R10G10B10UnormPack32 => SKColorType.Bgra1010102,
                Format.A2R10G10B10SNormPack32 => SKColorType.Unknown,
                Format.A2R10G10B10UscaledPack32 => SKColorType.Unknown,
                Format.A2R10G10B10SscaledPack32 => SKColorType.Unknown,
                Format.A2R10G10B10UintPack32 => SKColorType.Unknown,
                Format.A2R10G10B10SintPack32 => SKColorType.Unknown,
                Format.A2B10G10R10UnormPack32 => SKColorType.Rgba1010102,
                Format.A2B10G10R10SNormPack32 => SKColorType.Unknown,
                Format.A2B10G10R10UscaledPack32 => SKColorType.Unknown,
                Format.A2B10G10R10SscaledPack32 => SKColorType.Unknown,
                Format.A2B10G10R10UintPack32 => SKColorType.Unknown,
                Format.A2B10G10R10SintPack32 => SKColorType.Unknown,
                Format.R16Unorm => SKColorType.Alpha16,
                Format.R16SNorm => SKColorType.Unknown,
                Format.R16Uscaled => SKColorType.Unknown,
                Format.R16Sscaled => SKColorType.Unknown,
                Format.R16Uint => SKColorType.Unknown,
                Format.R16Sint => SKColorType.Unknown,
                Format.R16Sfloat => SKColorType.AlphaF16,
                Format.R16G16Unorm => SKColorType.Rg1616,
                Format.R16G16SNorm => SKColorType.Unknown,
                Format.R16G16Uscaled => SKColorType.Unknown,
                Format.R16G16Sscaled => SKColorType.Unknown,
                Format.R16G16Uint => SKColorType.Unknown,
                Format.R16G16Sint => SKColorType.Unknown,
                Format.R16G16Sfloat => SKColorType.RgF16,
                Format.R16G16B16Unorm => SKColorType.Unknown,
                Format.R16G16B16SNorm => SKColorType.Unknown,
                Format.R16G16B16Uscaled => SKColorType.Unknown,
                Format.R16G16B16Sscaled => SKColorType.Unknown,
                Format.R16G16B16Uint => SKColorType.Unknown,
                Format.R16G16B16Sint => SKColorType.Unknown,
                Format.R16G16B16Sfloat => SKColorType.Unknown,
                Format.R16G16B16A16Unorm => SKColorType.Rgba16161616,
                Format.R16G16B16A16SNorm => SKColorType.Unknown,
                Format.R16G16B16A16Uscaled => SKColorType.Unknown,
                Format.R16G16B16A16Sscaled => SKColorType.Unknown,
                Format.R16G16B16A16Uint => SKColorType.Unknown,
                Format.R16G16B16A16Sint => SKColorType.Unknown,
                Format.R16G16B16A16Sfloat => SKColorType.RgbaF16,
                Format.R32Uint => SKColorType.Unknown,
                Format.R32Sint => SKColorType.Unknown,
                Format.R32Sfloat => SKColorType.Unknown,
                Format.R32G32Uint => SKColorType.Unknown,
                Format.R32G32Sint => SKColorType.Unknown,
                Format.R32G32Sfloat => SKColorType.Unknown,
                Format.R32G32B32Uint => SKColorType.Unknown,
                Format.R32G32B32Sint => SKColorType.Unknown,
                Format.R32G32B32Sfloat => SKColorType.Unknown,
                Format.R32G32B32A32Uint => SKColorType.Unknown,
                Format.R32G32B32A32Sint => SKColorType.Unknown,
                Format.R32G32B32A32Sfloat => SKColorType.RgbaF32,
                Format.R64Uint => SKColorType.Unknown,
                Format.R64Sint => SKColorType.Unknown,
                Format.R64Sfloat => SKColorType.Unknown,
                Format.R64G64Uint => SKColorType.Unknown,
                Format.R64G64Sint => SKColorType.Unknown,
                Format.R64G64Sfloat => SKColorType.Unknown,
                Format.R64G64B64Uint => SKColorType.Unknown,
                Format.R64G64B64Sint => SKColorType.Unknown,
                Format.R64G64B64Sfloat => SKColorType.Unknown,
                Format.R64G64B64A64Uint => SKColorType.Unknown,
                Format.R64G64B64A64Sint => SKColorType.Unknown,
                Format.R64G64B64A64Sfloat => SKColorType.Unknown,
                Format.B10G11R11UfloatPack32 => SKColorType.Unknown,
                Format.E5B9G9R9UfloatPack32 => SKColorType.Unknown,
                Format.D16Unorm => SKColorType.Unknown,
                Format.X8D24UnormPack32 => SKColorType.Unknown,
                Format.D32Sfloat => SKColorType.Unknown,
                Format.S8Uint => SKColorType.Unknown,
                Format.D16UnormS8Uint => SKColorType.Unknown,
                Format.D24UnormS8Uint => SKColorType.Unknown,
                Format.D32SfloatS8Uint => SKColorType.Unknown,
                Format.BC1RgbUnormBlock => SKColorType.Unknown,
                Format.BC1RgbSrgbBlock => SKColorType.Unknown,
                Format.BC1RgbaUnormBlock => SKColorType.Unknown,
                Format.BC1RgbaSrgbBlock => SKColorType.Unknown,
                Format.BC2UnormBlock => SKColorType.Unknown,
                Format.BC2SrgbBlock => SKColorType.Unknown,
                Format.BC3UnormBlock => SKColorType.Unknown,
                Format.BC3SrgbBlock => SKColorType.Unknown,
                Format.BC4UnormBlock => SKColorType.Unknown,
                Format.BC4SNormBlock => SKColorType.Unknown,
                Format.BC5UnormBlock => SKColorType.Unknown,
                Format.BC5SNormBlock => SKColorType.Unknown,
                Format.BC6HUfloatBlock => SKColorType.Unknown,
                Format.BC6HSfloatBlock => SKColorType.Unknown,
                Format.BC7UnormBlock => SKColorType.Unknown,
                Format.BC7SrgbBlock => SKColorType.Unknown,
                Format.Etc2R8G8B8UnormBlock => SKColorType.Unknown,
                Format.Etc2R8G8B8SrgbBlock => SKColorType.Unknown,
                Format.Etc2R8G8B8A1UnormBlock => SKColorType.Unknown,
                Format.Etc2R8G8B8A1SrgbBlock => SKColorType.Unknown,
                Format.Etc2R8G8B8A8UnormBlock => SKColorType.Unknown,
                Format.Etc2R8G8B8A8SrgbBlock => SKColorType.Unknown,
                Format.EacR11UnormBlock => SKColorType.Unknown,
                Format.EacR11SNormBlock => SKColorType.Unknown,
                Format.EacR11G11UnormBlock => SKColorType.Unknown,
                Format.EacR11G11SNormBlock => SKColorType.Unknown,
                Format.Astc4x4UnormBlock => SKColorType.Unknown,
                Format.Astc4x4SrgbBlock => SKColorType.Unknown,
                Format.Astc5x4UnormBlock => SKColorType.Unknown,
                Format.Astc5x4SrgbBlock => SKColorType.Unknown,
                Format.Astc5x5UnormBlock => SKColorType.Unknown,
                Format.Astc5x5SrgbBlock => SKColorType.Unknown,
                Format.Astc6x5UnormBlock => SKColorType.Unknown,
                Format.Astc6x5SrgbBlock => SKColorType.Unknown,
                Format.Astc6x6UnormBlock => SKColorType.Unknown,
                Format.Astc6x6SrgbBlock => SKColorType.Unknown,
                Format.Astc8x5UnormBlock => SKColorType.Unknown,
                Format.Astc8x5SrgbBlock => SKColorType.Unknown,
                Format.Astc8x6UnormBlock => SKColorType.Unknown,
                Format.Astc8x6SrgbBlock => SKColorType.Unknown,
                Format.Astc8x8UnormBlock => SKColorType.Unknown,
                Format.Astc8x8SrgbBlock => SKColorType.Unknown,
                Format.Astc10x5UnormBlock => SKColorType.Unknown,
                Format.Astc10x5SrgbBlock => SKColorType.Unknown,
                Format.Astc10x6UnormBlock => SKColorType.Unknown,
                Format.Astc10x6SrgbBlock => SKColorType.Unknown,
                Format.Astc10x8UnormBlock => SKColorType.Unknown,
                Format.Astc10x8SrgbBlock => SKColorType.Unknown,
                Format.Astc10x10UnormBlock => SKColorType.Unknown,
                Format.Astc10x10SrgbBlock => SKColorType.Unknown,
                Format.Astc12x10UnormBlock => SKColorType.Unknown,
                Format.Astc12x10SrgbBlock => SKColorType.Unknown,
                Format.Astc12x12UnormBlock => SKColorType.Unknown,
                Format.Astc12x12SrgbBlock => SKColorType.Unknown,
                Format.Pvrtc12BppUnormBlockImg => SKColorType.Unknown,
                Format.Pvrtc14BppUnormBlockImg => SKColorType.Unknown,
                Format.Pvrtc22BppUnormBlockImg => SKColorType.Unknown,
                Format.Pvrtc24BppUnormBlockImg => SKColorType.Unknown,
                Format.Pvrtc12BppSrgbBlockImg => SKColorType.Unknown,
                Format.Pvrtc14BppSrgbBlockImg => SKColorType.Unknown,
                Format.Pvrtc22BppSrgbBlockImg => SKColorType.Unknown,
                Format.Pvrtc24BppSrgbBlockImg => SKColorType.Unknown,
                Format.Astc4x4SfloatBlockExt => SKColorType.Unknown,
                Format.Astc5x4SfloatBlockExt => SKColorType.Unknown,
                Format.Astc5x5SfloatBlockExt => SKColorType.Unknown,
                Format.Astc6x5SfloatBlockExt => SKColorType.Unknown,
                Format.Astc6x6SfloatBlockExt => SKColorType.Unknown,
                Format.Astc8x5SfloatBlockExt => SKColorType.Unknown,
                Format.Astc8x6SfloatBlockExt => SKColorType.Unknown,
                Format.Astc8x8SfloatBlockExt => SKColorType.Unknown,
                Format.Astc10x5SfloatBlockExt => SKColorType.Unknown,
                Format.Astc10x6SfloatBlockExt => SKColorType.Unknown,
                Format.Astc10x8SfloatBlockExt => SKColorType.Unknown,
                Format.Astc10x10SfloatBlockExt => SKColorType.Unknown,
                Format.Astc12x10SfloatBlockExt => SKColorType.Unknown,
                Format.Astc12x12SfloatBlockExt => SKColorType.Unknown,
                Format.G8B8G8R8422UnormKhr => SKColorType.Unknown,
                Format.B8G8R8G8422UnormKhr => SKColorType.Unknown,
                Format.G8B8R83Plane420UnormKhr => SKColorType.Unknown,
                Format.G8B8R82Plane420UnormKhr => SKColorType.Unknown,
                Format.G8B8R83Plane422UnormKhr => SKColorType.Unknown,
                Format.G8B8R82Plane422UnormKhr => SKColorType.Unknown,
                Format.G8B8R83Plane444UnormKhr => SKColorType.Unknown,
                Format.R10X6UnormPack16Khr => SKColorType.Unknown,
                Format.R10X6G10X6Unorm2Pack16Khr => SKColorType.Unknown,
                Format.R10X6G10X6B10X6A10X6Unorm4Pack16Khr => SKColorType.Rgba10x6,
                Format.G10X6B10X6G10X6R10X6422Unorm4Pack16Khr => SKColorType.Unknown,
                Format.B10X6G10X6R10X6G10X6422Unorm4Pack16Khr => SKColorType.Unknown,
                Format.G10X6B10X6R10X63Plane420Unorm3Pack16Khr => SKColorType.Unknown,
                Format.G10X6B10X6R10X62Plane420Unorm3Pack16Khr => SKColorType.Unknown,
                Format.G10X6B10X6R10X63Plane422Unorm3Pack16Khr => SKColorType.Unknown,
                Format.G10X6B10X6R10X62Plane422Unorm3Pack16Khr => SKColorType.Unknown,
                Format.G10X6B10X6R10X63Plane444Unorm3Pack16Khr => SKColorType.Unknown,
                Format.R12X4UnormPack16Khr => SKColorType.Unknown,
                Format.R12X4G12X4Unorm2Pack16Khr => SKColorType.Unknown,
                Format.R12X4G12X4B12X4A12X4Unorm4Pack16Khr => SKColorType.Unknown,
                Format.G12X4B12X4G12X4R12X4422Unorm4Pack16Khr => SKColorType.Unknown,
                Format.B12X4G12X4R12X4G12X4422Unorm4Pack16Khr => SKColorType.Unknown,
                Format.G12X4B12X4R12X43Plane420Unorm3Pack16Khr => SKColorType.Unknown,
                Format.G12X4B12X4R12X42Plane420Unorm3Pack16Khr => SKColorType.Unknown,
                Format.G12X4B12X4R12X43Plane422Unorm3Pack16Khr => SKColorType.Unknown,
                Format.G12X4B12X4R12X42Plane422Unorm3Pack16Khr => SKColorType.Unknown,
                Format.G12X4B12X4R12X43Plane444Unorm3Pack16Khr => SKColorType.Unknown,
                Format.G16B16G16R16422UnormKhr => SKColorType.Unknown,
                Format.B16G16R16G16422UnormKhr => SKColorType.Unknown,
                Format.G16B16R163Plane420UnormKhr => SKColorType.Unknown,
                Format.G16B16R162Plane420UnormKhr => SKColorType.Unknown,
                Format.G16B16R163Plane422UnormKhr => SKColorType.Unknown,
                Format.G16B16R162Plane422UnormKhr => SKColorType.Unknown,
                Format.G16B16R163Plane444UnormKhr => SKColorType.Unknown,
                Format.G8B8R82Plane444UnormExt => SKColorType.Unknown,
                Format.G10X6B10X6R10X62Plane444Unorm3Pack16Ext => SKColorType.Unknown,
                Format.G12X4B12X4R12X42Plane444Unorm3Pack16Ext => SKColorType.Unknown,
                Format.G16B16R162Plane444UnormExt => SKColorType.Unknown,
                Format.A4R4G4B4UnormPack16Ext => SKColorType.Unknown,
                Format.A4B4G4R4UnormPack16Ext => SKColorType.Unknown,
                Format.R8BoolArm => SKColorType.Unknown,
                Format.R16G16Sfixed5NV => SKColorType.Unknown,
                Format.A1B5G5R5UnormPack16Khr => SKColorType.Unknown,
                Format.A8UnormKhr => SKColorType.Alpha8,
                Format.R10X6UintPack16Arm => SKColorType.Unknown,
                Format.R10X6G10X6Uint2Pack16Arm => SKColorType.Unknown,
                Format.R10X6G10X6B10X6A10X6Uint4Pack16Arm => SKColorType.Unknown,
                Format.R12X4UintPack16Arm => SKColorType.Unknown,
                Format.R12X4G12X4Uint2Pack16Arm => SKColorType.Unknown,
                Format.R12X4G12X4B12X4A12X4Uint4Pack16Arm => SKColorType.Unknown,
                Format.R14X2UintPack16Arm => SKColorType.Unknown,
                Format.R14X2G14X2Uint2Pack16Arm => SKColorType.Unknown,
                Format.R14X2G14X2B14X2A14X2Uint4Pack16Arm => SKColorType.Unknown,
                Format.R14X2UnormPack16Arm => SKColorType.Unknown,
                Format.R14X2G14X2Unorm2Pack16Arm => SKColorType.Unknown,
                Format.R14X2G14X2B14X2A14X2Unorm4Pack16Arm => SKColorType.Unknown,
                Format.G14X2B14X2R14X22Plane420Unorm3Pack16Arm => SKColorType.Unknown,
                Format.G14X2B14X2R14X22Plane422Unorm3Pack16Arm => SKColorType.Unknown,
                _ => throw new ArgumentOutOfRangeException(nameof(vkFormat), vkFormat, null),
            };
    }
}
