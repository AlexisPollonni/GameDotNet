using System.Drawing;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using GameDotNet.Graphics;
using ShaderSlang.Net.ComWrappers.Gfx.Descriptions;
using ShaderSlang.Net.ComWrappers.Gfx.Interfaces;
using static Avalonia.Platform.KnownPlatformGraphicsExternalSemaphoreHandleTypes;
using ICommandQueue = ShaderSlang.Net.ComWrappers.Gfx.Interfaces.ICommandQueue;
using IFence = ShaderSlang.Net.ComWrappers.Gfx.Interfaces.IFence;
using ITextureResource = ShaderSlang.Net.ComWrappers.Gfx.Interfaces.ITextureResource;
using ITransientResourceHeap = ShaderSlang.Net.ComWrappers.Gfx.Interfaces.ITransientResourceHeap;
using Unmanaged = ShaderSlang.Net.Bindings.Generated;

namespace GameDotNet.Editor.Tools.GpuInterop;

internal class SlangGfxSwapchain(ICompositionGpuInterop interop, CompositionDrawingSurface target, SlangContext context)
    : SwapchainBase<SlangGfxSwapchainImage>(interop, target)
{
    protected override SlangGfxSwapchainImage CreateImage(PixelSize size) =>
        new(context, size, Interop, Target);

    public IDisposable BeginDraw(PixelSize size, out ITextureResource texture)
    {
        var rv = BeginDrawCore(size, out var swapchainImage);
        texture = swapchainImage.Texture;
        return rv;
    }
}

internal class SlangGfxSwapchainImage : ISwapchainImage
{
    private readonly SlangContext _context;
    private readonly IFence _imageAvailableFence, _renderCompletedFence;
    private readonly ICommandQueue _queue;
    private readonly ITransientResourceHeap _heap;

    private readonly ICompositionGpuInterop _interop;
    private readonly CompositionDrawingSurface _target;
    private ICompositionImportedGpuSemaphore? _availableSemaphore, _renderCompletedSemaphore;
    private ICompositionImportedGpuImage? _importedImage;

    private bool _initial = true;

    internal ITextureResource Texture { get; }
    public PixelSize Size { get; }
    public Task? LastPresent { get; private set; }

    public SlangGfxSwapchainImage(SlangContext context, PixelSize size, ICompositionGpuInterop interop, CompositionDrawingSurface target)
    {
        _context = context;
        Size = size;
        _interop = interop;
        _target = target;

        var textureDesc = new TextureResourceDescription(new(Unmanaged.IResource.ResourceType.Texture2D,
                                                             Unmanaged.ResourceState.Undefined,
                                                             new(Unmanaged.ResourceState.Undefined,
                                                                 Unmanaged.ResourceState.CopyDestination,
                                                                 Unmanaged.ResourceState.Present,
                                                                 Unmanaged.ResourceState.RenderTarget),
                                                             Unmanaged.MemoryType.DeviceLocal,
                                                             IsShared: true),
                                                         new()
                                                         {
                                                             width = size.Width,
                                                             height = size.Height,
                                                             depth = 1
                                                         },
                                                         Unmanaged.Format.R8G8B8A8_UNORM,
                                                         new(),
                                                         new(Color.Blue));
        
        Texture = _context.Device.CreateTextureResourceOrThrow(textureDesc, new(Memory<byte>.Empty, 0, 0));

        _imageAvailableFence = _context.Device.CreateFenceOrThrow(new(IsShared: true));
        _renderCompletedFence = _context.Device.CreateFenceOrThrow(new(IsShared: true));
        _queue = _context.Device.CreateCommandQueueOrThrow(new(Unmanaged.ICommandQueue.QueueType.Graphics));
        _heap = _context.Device.CreateTransientResourceHeapOrThrow(new());
    }

    public async ValueTask DisposeAsync()
    {
        if (LastPresent != null)
            await LastPresent;
        if (_importedImage != null)
            await _importedImage.DisposeAsync();
        if (_availableSemaphore != null)
            await _availableSemaphore.DisposeAsync();
        if (_renderCompletedSemaphore != null) 
            await _renderCompletedSemaphore.DisposeAsync();
    }

    public void BeginDraw()
    {
        var buffer = _heap.CreateCommandBufferOrThrow();
        buffer.EncodeResourceCommands(out var encoder);
        encoder.TextureBarrier(1, [Texture], Unmanaged.ResourceState.Undefined, Unmanaged.ResourceState.RenderTarget);
        encoder.EndEncoding();
        buffer.Close();
        if (_initial)
        {
            _initial = false;
            _queue.ExecuteCommandBuffers(1, [buffer]);
        }else
            _queue.ExecuteCommandBuffers(1, [buffer], _imageAvailableFence);
    }
    public void Present()
    {
        var buffer = _heap.CreateCommandBufferOrThrow();
        buffer.EncodeResourceCommands(out var encoder);
        
        encoder.TextureBarrier(1, [Texture], Unmanaged.ResourceState.RenderTarget, Unmanaged.ResourceState.CopySource);
        
        encoder.EndEncoding();
        buffer.Close();
        
        _queue.ExecuteCommandBuffers(1, [buffer], _renderCompletedFence);

        _availableSemaphore ??= _interop.ImportSemaphore(ExportSemaphore(_imageAvailableFence));
        _renderCompletedSemaphore ??= _interop.ImportSemaphore(ExportSemaphore(_renderCompletedFence));

        _importedImage ??= _interop.ImportImage(ExportImage(Texture, out var props), props);

        LastPresent = _target.UpdateWithSemaphoresAsync(_importedImage, _renderCompletedSemaphore, _availableSemaphore);
    }

    private static IPlatformHandle ExportSemaphore(IFence fence)
    {
        var handle = fence.GetSharedHandleOrThrow();

        var type = handle?.api switch
        {
            Unmanaged.InteropHandleAPI.Vulkan => OperatingSystem.IsWindows() ? VulkanOpaqueNtHandle : VulkanOpaquePosixFileDescriptor,
            Unmanaged.InteropHandleAPI.D3D12 => Direct3D12FenceNtHandle,
            _ => throw new PlatformNotSupportedException(
                $"Avalonia graphics interop synchronisation not supported for api: {handle?.api}")
        };

        return new PlatformHandle((nint)handle.Value.handleValue, type);
    }

    private  IPlatformHandle ExportImage(ITextureResource texture, out PlatformGraphicsExternalImageProperties externalProperties)
    {
        var desc = texture.GetDesc();
        _context.Device.GetTextureAllocationInfo(desc, out var textureAllocSize, out var textureAlignment)
                .ThrowIfFailed();

        var handle = texture.GetSharedHandleOrThrow();
        var type = handle?.api switch
        {
            Unmanaged.InteropHandleAPI.Vulkan => OperatingSystem.IsWindows()
                ? KnownPlatformGraphicsExternalImageHandleTypes.VulkanOpaqueNtHandle
                : KnownPlatformGraphicsExternalImageHandleTypes.VulkanOpaquePosixFileDescriptor,

            Unmanaged.InteropHandleAPI.D3D12 => KnownPlatformGraphicsExternalImageHandleTypes.D3D11TextureNtHandle,
            
            _ => throw new PlatformNotSupportedException(
                $"Avalonia graphics image interop not supported for api: {handle?.api}")
        };

        externalProperties = new()
        {
            Format = PlatformGraphicsExternalImageFormat.R8G8B8A8UNorm,
            Width = desc.Size.width,
            Height = desc.Size.height,
            MemorySize = textureAllocSize,
            MemoryOffset = textureAlignment,
            TopLeftOrigin = true
        };
        
        return new PlatformHandle((nint)handle?.handleValue, type);
    }
}