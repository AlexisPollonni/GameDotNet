using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Rendering;
using Avalonia.Rendering.Composition;
using GameDotNet.Editor.VulkanDemo;
using GameDotNet.Graphics.Vulkan.Tools.Extensions;
using GameDotNet.Graphics.Vulkan.Wrappers;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Format = Silk.NET.Vulkan.Format;

namespace GpuInterop.VulkanDemo;

class VulkanSwapchain : SwapchainBase<VulkanSwapchainImage>
{
    private readonly VulkanContext _vk;

    public VulkanSwapchain(VulkanContext vk, ICompositionGpuInterop interop, CompositionDrawingSurface target) :
        base(interop, target)
    {
        _vk = vk;
    }

    protected override VulkanSwapchainImage CreateImage(PixelSize size)
    {
        return new(_vk, size, Interop, Target);
    }

    public IDisposable BeginDraw(PixelSize size, out VulkanImage image)
    {
        _vk.Pool.FreeUsedCommandBuffers();
        var rv = BeginDrawCore(size, out var swapchainImage);
        image = swapchainImage.Image;
        return rv;
    }
}

internal class VulkanSwapchainImage : ISwapchainImage
{
    private readonly VulkanContext _vk;
    private readonly ICompositionGpuInterop _interop;
    private readonly CompositionDrawingSurface _target;
    private readonly VulkanSemaphorePair _semaphorePair;
    private ICompositionImportedGpuSemaphore? _availableSemaphore, _renderCompletedSemaphore;
    private ICompositionImportedGpuImage? _importedImage;
    private Task? _lastPresent;
    private readonly Texture2D? _d3dTex2D;
    public VulkanImage Image { get; }

    private bool _initial = true;

    public VulkanSwapchainImage(VulkanContext vk, PixelSize size, ICompositionGpuInterop interop,
                                CompositionDrawingSurface target)
    {
        _vk = vk;
        _interop = interop;
        _target = target;
        Size = size;
        _semaphorePair = new(vk.Instance, vk.Device, true);

        var (img, texture) = vk.CreateVulkanImage((uint)Format.R8G8B8A8Unorm, size, true);
        Image = img;
        _d3dTex2D = texture;
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
        _semaphorePair.Dispose();
        Image.Dispose();
    }

    public PixelSize Size { get; }

    public Task? LastPresent => _lastPresent;

    public void BeginDraw()
    {
        var buffer = _vk.Pool.CreateCommandBuffer();
        buffer.BeginRecording();

        Image.TransitionLayout(buffer,
                               ImageLayout.Undefined, AccessFlags.None,
                               ImageLayout.ColorAttachmentOptimal, AccessFlags.ColorAttachmentReadBit);

        if (OperatingSystem.IsWindows())
            buffer.Submit(null, null, null, null, new()
            {
                AcquireKey = 0,
                DeviceMemory = Image.Allocation.DeviceMemory
            });
        else if (_initial)
        {
            _initial = false;
            buffer.Submit();
        }
        else
            buffer.Submit(new[] { _semaphorePair.ImageAvailableSemaphore },
                          new[]
                          {
                              PipelineStageFlags.AllGraphicsBit
                          });
    }


    public void Present()
    {
        var isWin = OperatingSystem.IsWindows();
        var buffer = _vk.Pool.CreateCommandBuffer();
        buffer.BeginRecording();
        Image.TransitionLayout(buffer, ImageLayout.TransferSrcOptimal, AccessFlags.TransferWriteBit);

        if (isWin)
        {
            buffer.Submit(null, null, null, null,
                          new()
                          {
                              DeviceMemory = Image.Allocation.DeviceMemory, ReleaseKey = 1
                          });
        }
        else
            buffer.Submit(null, null, new[] { _semaphorePair.RenderFinishedSemaphore });

        if (!isWin)
        {
            _availableSemaphore ??= _interop.ImportSemaphore(new PlatformHandle(
                                                                                new(_semaphorePair.ExportFd(false)),
                                                                                KnownPlatformGraphicsExternalSemaphoreHandleTypes
                                                                                    .VulkanOpaquePosixFileDescriptor));

            _renderCompletedSemaphore ??= _interop.ImportSemaphore(new PlatformHandle(
                                                                    new(_semaphorePair.ExportFd(true)),
                                                                    KnownPlatformGraphicsExternalSemaphoreHandleTypes
                                                                        .VulkanOpaquePosixFileDescriptor));
        }

        _importedImage ??= _interop.ImportImage(Export(),
                                                new()
                                                {
                                                    Format = PlatformGraphicsExternalImageFormat.R8G8B8A8UNorm,
                                                    Width = Size.Width,
                                                    Height = Size.Height,
                                                    MemorySize = (ulong)Image.Allocation.Size
                                                });

        if (isWin)
            _lastPresent = _target.UpdateWithKeyedMutexAsync(_importedImage, 1, 0);
        else
            _lastPresent =
                _target.UpdateWithSemaphoresAsync(_importedImage, _renderCompletedSemaphore!, _availableSemaphore!);
    }

    private int ExportFd()
    {
        if (!_vk.Api.TryGetDeviceExtension<KhrExternalMemoryFd>(_vk.Instance, _vk.Device, out var ext))
            throw new InvalidOperationException();
        var info = new MemoryGetFdInfoKHR
        {
            Memory = Image.Allocation.DeviceMemory,
            SType = StructureType.MemoryGetFDInfoKhr,
            HandleType = ExternalMemoryHandleTypeFlags.OpaqueFDBit
        };
        ext.GetMemoryF(_vk.Device, info, out var fd).ThrowOnError();
        return fd;
    }

    private IPlatformHandle Export()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new PlatformHandle(new(ExportFd()),
                                      KnownPlatformGraphicsExternalImageHandleTypes.VulkanOpaquePosixFileDescriptor);

        using var dxgi = _d3dTex2D!.QueryInterface<Resource1>();
        return new PlatformHandle(dxgi.CreateSharedHandle(null, SharedResourceFlags.Read | SharedResourceFlags.Write),
                                  KnownPlatformGraphicsExternalImageHandleTypes.D3D11TextureNtHandle);
    }
}