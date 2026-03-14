using Avalonia;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using GameDotNet.Core.Tooling;
using GameDotNet.Graphics.Abstractions;
using GameDotNet.Graphics.Avalonia.Gpu.Interop;
using GameDotNet.Graphics.Vulkan.Wrappers;
using Nito.Disposables;
using Silk.NET.Vulkan;
using static Avalonia.Platform.KnownPlatformGraphicsExternalSemaphoreHandleTypes;

namespace GameDotNet.Graphics.Vulkan;

//TODO: to internal and instantiate with DI
public sealed class VulkanAvaloniaSwapchain(
    ICompositionGpuInterop interop,
    CompositionDrawingSurface target,
    IVulkanContext context
) : SwapchainBase(interop, target)
{
    protected override VulkanAvaloniaSwapchainImage CreateImage(PixelSize size) =>
        new(context, size, Interop, Target);

    public override IDisposable BeginDraw(PixelSize size, out ISwapchainImage image)
    {
        context.Pool.FreeUsedCommandBuffers();
        var rv = BeginDrawCore(size, out image);
        return rv;
    }
}

public class VulkanAvaloniaSwapchainImage(
    IVulkanContext context,
    PixelSize size,
    ICompositionGpuInterop interop,
    CompositionDrawingSurface target
) : SingleNonblockingAsyncDisposable<EmptyStruct>(default), ISwapchainImage, IDeviceTexture
{
    private readonly VulkanSemaphorePair _semaphorePair = new(context, true);

    private ICompositionImportedGpuSemaphore? _availableSemaphore,
        _renderCompletedSemaphore;
    private ICompositionImportedGpuImage? _importedImage;

    private bool _initial = true;

    internal VulkanImage Image { get; } =
        VulkanImage.CreateExportableImage(
            context.Device,
            Format.R8G8B8A8Unorm,
            ImageUsageFlags.ColorAttachmentBit
                | ImageUsageFlags.TransferSrcBit
                | ImageUsageFlags.TransferDstBit
                | ImageUsageFlags.SampledBit,
            new((uint)size.Width, (uint)size.Height, 1),
            OperatingSystem.IsWindows()
                ? ExternalMemoryHandleTypeFlags.OpaqueWin32Bit
                : ExternalMemoryHandleTypeFlags.OpaqueFDBit
        );

    public PixelSize Size { get; } = size;
    public Task? LastPresent { get; private set; }
    public TextureDescription Description => Image.Description;

    protected override async ValueTask DisposeAsync(EmptyStruct _)
    {
        if (LastPresent != null)
            await LastPresent;
        if (_importedImage != null)
            await _importedImage.DisposeAsync();
        if (_availableSemaphore != null)
            await _availableSemaphore.DisposeAsync();
        if (_renderCompletedSemaphore != null)
            await _renderCompletedSemaphore.DisposeAsync();

        _semaphorePair.Dispose();
        Image.Dispose();
    }

    public void BeginDraw()
    {
        var cmd = context.Pool.CreateCommandBuffer();

        cmd.BeginRecording();

        Image.TransitionLayout(
            cmd,
            ImageLayout.ColorAttachmentOptimal,
            AccessFlags.ColorAttachmentReadBit
        );

        if (_initial)
        {
            _initial = false;
            cmd.Submit();
        }
        else
        {
            cmd.Submit(_semaphorePair.ImageAvailableSemaphore, PipelineStageFlags.AllGraphicsBit);
        }
    }

    public void Present()
    {
        //TODO: handle timeline semaphores for metal
        var cmd = context.Pool.CreateCommandBuffer();
        cmd.BeginRecording();
        Image.TransitionLayout(cmd, ImageLayout.TransferSrcOptimal, AccessFlags.TransferWriteBit);

        cmd.Submit(signal: _semaphorePair.RenderFinishedSemaphore);

        _availableSemaphore ??= interop.ImportSemaphore(ExportSemaphore(_semaphorePair, false));
        _renderCompletedSemaphore ??= interop.ImportSemaphore(
            ExportSemaphore(_semaphorePair, true)
        );

        _importedImage ??= interop.ImportImage(ExportImage(Image, out var props), props);

        LastPresent = target.UpdateWithSemaphoresAsync(
            _importedImage,
            _renderCompletedSemaphore,
            _availableSemaphore
        );
    }

    private static IPlatformHandle ExportSemaphore(
        VulkanSemaphorePair semPair,
        bool isRenderFinished
    )
    {
        var handle = semPair.Export(isRenderFinished);

        var type = OperatingSystem.IsWindows()
            ? VulkanOpaqueNtHandle
            : VulkanOpaquePosixFileDescriptor;

        return new PlatformHandle(handle, type);
    }

    private static IPlatformHandle ExportImage(
        VulkanImage texture,
        out PlatformGraphicsExternalImageProperties externalProperties
    )
    {
        var handle = texture.ExportPlatformHandLe();
        var type = OperatingSystem.IsWindows()
            ? KnownPlatformGraphicsExternalImageHandleTypes.VulkanOpaqueNtHandle
            : KnownPlatformGraphicsExternalImageHandleTypes.VulkanOpaquePosixFileDescriptor;

        externalProperties = new()
        {
            Format = PlatformGraphicsExternalImageFormat.R8G8B8A8UNorm,
            Width = (int)texture.Extent.Width,
            Height = (int)texture.Extent.Height,
            MemorySize = (ulong)texture.Allocation.Size,
            MemoryOffset = (ulong)texture.Allocation.Alignment,
            TopLeftOrigin = true,
        };

        return new PlatformHandle(handle, type);
    }
}
