using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using Avalonia.Skia;
using Avalonia.Threading;
using GameDotNet.Core.Abstractions;
using GameDotNet.Core.Tooling;
using GameDotNet.Editor.ViewModels;
using GameDotNet.Graphics.Abstractions;
using GameDotNet.Graphics.Avalonia;
using GameDotNet.Graphics.Avalonia.Gpu.Interop;
using GameDotNet.Graphics.Models;
using GameDotNet.Graphics.Tooling;
using GameDotNet.Graphics.Vulkan;
using GameDotNet.Graphics.Vulkan.MemoryAllocation;
using GameDotNet.Graphics.Vulkan.Wrappers;
using MessagePipe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Nito.Disposables;
using ReactiveUI.Avalonia;
using Shouldly;
using Silk.NET.Vulkan;
using SkiaSharp;
using Size = System.Drawing.Size;

namespace GameDotNet.Editor.Views;

public partial class EditorTabViewPortControl : ReactiveUserControl<EditorTabViewPortViewModel>
{
    public EditorTabViewPortControl()
    {
        InitializeComponent();
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();

        var sp = App.GetServiceProvider().ShouldNotBeNull();

        //I do not have a mac device but for macos we will need to go the route of gpu interop
        Content = sp.GetRequiredService<RenderThreadAnimationControl>();
    }
}

[RegisterTransient<RenderThreadAnimationControl>]
public class RenderThreadAnimationControl(
    ILogger<RenderThreadAnimationControl> logger,
    IVulkanContext context,
    IAsyncRequestHandler<RenderFrameRequest, RenderFramePresentResponse> renderHandler,
    ObjectPool<PooledValueTaskSource> tcsPool,
    IEventBus eventBus
) : AvaloniaViewPortControl(logger, eventBus)
{
    private CompositionCustomVisual? _customVisual;
    private CancellationTokenSource _cts = new();
    private Task? _renderTask;
    private CustomVisualHandler? _handler;
    private SkiaSwapchainImage? _presentImage;
    private readonly Channel<SkiaFrameReady> _swapchainChannel =
        Channel.CreateBounded<SkiaFrameReady>(
            new BoundedChannelOptions(1) //TODO: explore buffering
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false,
            }
        );

    public TimelineStats? RenderStats { get; private set; }

    private class CustomVisualHandler(ILogger logger, Channel<SkiaFrameReady> frameReadyChannel)
        : CompositionCustomVisualHandler
    {
        public override void OnRender(ImmediateDrawingContext drawingContext)
        {
            if (!frameReadyChannel.Reader.TryRead(out var frame))
            {
                RegisterForNextAnimationFrameUpdate();
                Invalidate();
                return;
            }

            var feature = drawingContext.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (feature is null)
                return;

            using var lease = feature.Lease();

            try
            {
                DrawCanvas(
                    lease.SkCanvas,
                    lease.GrContext.ShouldNotBeNull("No Gr Context"),
                    frame.Image
                );
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
            var vkImageInfo = image.ImageInfo;

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
                SKAlphaType.Opaque
            );

            if (skImage is not null)
            {
                var dest = new SKRect(0, 0, (float)EffectiveSize.X, (float)EffectiveSize.Y);
                canvas.DrawImage(skImage, dest);
            }
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

            _handler = new(logger, _swapchainChannel);
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

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty && _customVisual is not null)
        {
            _customVisual.Size = new(Bounds.Width, Bounds.Height);
        }
    }

    private async Task RenderLoopAsync(CancellationToken token)
    {
        //TODO: move this to the frame updater
        try
        {
            while (!token.IsCancellationRequested)
            {
                // Wait for the compositor to be ready
                await _swapchainChannel.Writer.WaitToWriteAsync(token).ConfigureAwait(false);

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

        if (_presentImage is null || _presentImage.Size != pixelSize)
        {
            await (_presentImage?.DisposeAsync() ?? ValueTask.CompletedTask).ConfigureAwait(false);

            _presentImage = new(context, pixelSize);
        }

        // The renderer writes to the VulkanImage (IDeviceTexture) the same as with the interop path.
        var presentResponse = await renderHandler
            .InvokeAsync(new(this, _presentImage.Image), token)
            .ConfigureAwait(false);

        // Transition to shader-readable
        _presentImage.Present();

        Dispatcher.UIThread.Invoke(() => RenderStats = presentResponse.RenderStats);
        await _swapchainChannel.Writer.WriteAsync(new(_presentImage), token).ConfigureAwait(false);
    }
}

internal class SkiaSwapchainImage : SingleNonblockingAsyncDisposable<EmptyStruct>, ISwapchainImage
{
    private readonly IVulkanContext _context;
    private readonly VulkanFence _renderFence;

    internal VulkanImage Image { get; }

    public PixelSize Size { get; }
    public Task? LastPresent { get; private set; }

    public GRVkImageInfo ImageInfo =>
        new()
        {
            Alloc = new()
            {
                Offset = (ulong)Image.Allocation.Offset,
                Size = (ulong)Image.Allocation.Size,
                BackendMemory = (IntPtr)Image.Allocation.DeviceMemory.Handle,
            },
            Image = Image.Image.Handle,
            ImageTiling = (uint)Image.CreateInfo.Tiling,
            ImageLayout = (uint)ImageLayout.ShaderReadOnlyOptimal,
            Format = (uint)Image.Format,
            ImageUsageFlags = (uint)Image.CreateInfo.Usage,
            SampleCount = (uint)Image.CreateInfo.Samples,
            LevelCount = Image.CreateInfo.MipLevels,
            Protected = Image.CreateInfo.Flags.HasFlag(ImageCreateFlags.CreateProtectedBit),
            SharingMode = (uint)Image.CreateInfo.SharingMode,
            CurrentQueueFamily = (uint)_context.Device.QueuesManager.GetFirstGraphic()!.FamilyIndex,
        };

    public SkiaSwapchainImage(IVulkanContext context, PixelSize size)
        : base(default)
    {
        _context = context;
        Size = size;

        // Normal image — no external memory flags needed
        var createInfo = VulkanImage.GetImageCreateInfo(
            Format.R8G8B8A8Unorm,
            ImageUsageFlags.ColorAttachmentBit
                | ImageUsageFlags.TransferSrcBit
                | ImageUsageFlags.TransferDstBit
                | ImageUsageFlags.SampledBit,
            new((uint)size.Width, (uint)size.Height, 1)
        );

        var allocInfo = new AllocationCreateInfo { Usage = MemoryUsage.GPU_Only };

        Image = new(context, in createInfo, in allocInfo);

        _renderFence = new(
            context.Api,
            context.Device,
            FenceCreateFlags.SignaledBit,
            context.Callbacks
        );
    }

    public void BeginDraw()
    {
        // Wait for previous frame's GPU work on this image to complete
        _renderFence.Wait();
        _renderFence.Reset();

        _context.Pool.FreeUsedCommandBuffers();

        using var cmd = _context.Pool.CreateCommandBuffer();
        cmd.BeginRecording();
        Image.TransitionLayout(
            cmd,
            ImageLayout.ColorAttachmentOptimal,
            AccessFlags.ColorAttachmentWriteBit
        );
        cmd.Submit();
    }

    public void Present()
    {
        // Transition to shader-readable for Skia sampling
        Image.TransitionLayout(
            _context.Pool,
            ImageLayout.ShaderReadOnlyOptimal,
            AccessFlags.ShaderReadBit
        );

        // LastPresent completes immediately since there's no async GPU interop
        LastPresent = Task.CompletedTask;
    }

    protected override ValueTask DisposeAsync(EmptyStruct _)
    {
        _renderFence.Wait(); // ensure GPU is done
        _renderFence.Dispose();
        Image.Dispose();
        return ValueTask.CompletedTask;
    }
}

internal record struct SkiaFrameReady(SkiaSwapchainImage Image);
