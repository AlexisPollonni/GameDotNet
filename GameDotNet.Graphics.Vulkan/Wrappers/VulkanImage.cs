using GameDotNet.Core.Tooling;
using GameDotNet.Graphics.Abstractions;
using GameDotNet.Graphics.Vulkan.MemoryAllocation;
using GameDotNet.Graphics.Vulkan.Tools.Extensions;
using Nito.Disposables;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace GameDotNet.Graphics.Vulkan.Wrappers;

public sealed class VulkanImage : SingleNonblockingDisposable<EmptyStruct>, IDeviceTexture
{
    public ImageCreateInfo CreateInfo => _info;
    public Image Image { get; }
    public Allocation Allocation { get; }

    public Format Format => _info.Format;
    public Extent3D Extent => _info.Extent;
    public TextureDescription Description =>
        new()
        {
            PixelSize = new((int)Extent.Width, (int)Extent.Height),
            Size = new(Allocation.Size),
            SampleCount = _info.Samples switch
            {
                SampleCountFlags.None => 0,
                SampleCountFlags.Count1Bit => 1,
                SampleCountFlags.Count2Bit => 2,
                SampleCountFlags.Count4Bit => 4,
                SampleCountFlags.Count8Bit => 8,
                SampleCountFlags.Count16Bit => 16,
                SampleCountFlags.Count32Bit => 32,
                SampleCountFlags.Count64Bit => 64,
                _ => throw new ArgumentOutOfRangeException(),
            },
        };

    private readonly IVulkanContext _context;

    private ImageCreateInfo _info;
    private AccessFlags _currentAccessFlags;

    public VulkanImage(
        IVulkanContext context,
        ref readonly ImageCreateInfo info,
        ref readonly AllocationCreateInfo allocInfo
    )
        : base(default)
    {
        _context = context;
        _info = info;

        Image = context.Allocator.CreateImage(info, allocInfo, out var alloc);
        Allocation = alloc;
    }

    public static implicit operator Image(VulkanImage img) => img.Image;

    public VulkanImageView GetImageView(ref readonly ImageViewCreateInfo info) =>
        new(_context, in info);

    public VulkanImageView GetImageView(Format format, ImageAspectFlags aspectFlags)
    {
        var createInfo = GetImageViewCreateInfo(format, Image, aspectFlags);

        return GetImageView(in createInfo);
    }

    public void TransitionLayout(
        CommandBuffer commandBuffer,
        ImageLayout fromLayout,
        AccessFlags fromAccessFlags,
        ImageLayout destinationLayout,
        AccessFlags destinationAccessFlags
    )
    {
        TransitionLayout(
            _context.Api,
            commandBuffer,
            Image,
            fromLayout,
            fromAccessFlags,
            destinationLayout,
            destinationAccessFlags,
            _info.MipLevels
        );

        _info.InitialLayout = destinationLayout;

        _currentAccessFlags = destinationAccessFlags;
    }

    public void TransitionLayout(
        CommandBuffer commandBuffer,
        ImageLayout destinationLayout,
        AccessFlags destinationAccessFlags
    ) =>
        TransitionLayout(
            commandBuffer,
            _info.InitialLayout,
            _currentAccessFlags,
            destinationLayout,
            destinationAccessFlags
        );

    public void TransitionLayout(
        VulkanCommandBufferPool pool,
        ImageLayout destinationLayout,
        AccessFlags destinationAccessFlags
    )
    {
        var commandBuffer = pool.CreateCommandBuffer();
        commandBuffer.BeginRecording();
        TransitionLayout(commandBuffer.Underlying, destinationLayout, destinationAccessFlags);
        commandBuffer.EndRecording();
        commandBuffer.Submit();
    }

    public void TransitionLayout(
        VulkanCommandBufferPool pool,
        uint destinationLayout,
        uint destinationAccessFlags
    )
    {
        TransitionLayout(pool, (ImageLayout)destinationLayout, (AccessFlags)destinationAccessFlags);
    }

    protected override void Dispose(EmptyStruct context)
    {
        Allocation.Dispose();
    }

    public static VulkanImage CreateExportableImage(
        VulkanDevice device,
        Format format,
        ImageUsageFlags usageFlags,
        Extent3D extent,
        ExternalMemoryHandleTypeFlags handleType
    )
    {
        var createInfo = GetImageCreateInfo(format, usageFlags, extent);

        //TODO: handle metal with ExportMetalObjectCreateInfoEXT
        createInfo.AddNext(out ExternalMemoryImageCreateInfo next);
        next.HandleTypes = handleType;

        var exportInfo = new ExportMemoryAllocateInfo { HandleTypes = handleType };
        using var chain = Chain.Create<MemoryAllocateInfo>().Add(exportInfo);

        var allocInfo = new AllocationCreateInfo
        {
            Flags = AllocationCreateFlags.DedicatedMemory,
            Usage = MemoryUsage.GPU_Only,
            MemoryAllocateNext = chain,
        };

        return new(device.Context, in createInfo, in allocInfo);
    }

    public nint ExportPlatformHandLe()
    {
        if (OperatingSystem.IsLinux())
        {
            var fdExt = _context.GetExtension<KhrExternalMemoryFd>();

            var info = new MemoryGetFdInfoKHR
            {
                SType = StructureType.MemoryGetFDInfoKhr,
                Memory = Allocation.DeviceMemory,
                HandleType = ExternalMemoryHandleTypeFlags.OpaqueFDBit,
            };
            fdExt.GetMemoryF(_context.Device, in info, out var fd).ThrowOnError();
            return fd;
        }

        if (OperatingSystem.IsWindows())
        {
            var win32Ext = _context.GetExtension<KhrExternalMemoryWin32>();

            var info = new MemoryGetWin32HandleInfoKHR
            {
                SType = StructureType.MemoryGetWin32HandleInfoKhr,
                Memory = Allocation.DeviceMemory,
                HandleType = ExternalMemoryHandleTypeFlags.OpaqueWin32Bit,
            };
            win32Ext.GetMemoryWin32Handle(_context.Device, in info, out var handle).ThrowOnError();
            return handle;
        }

        throw new PlatformNotSupportedException(
            "External memory export is not supported on this platform."
        );
    }

    public static unsafe ImageCreateInfo GetImageCreateInfo(
        Format format,
        ImageUsageFlags usageFlags,
        Extent3D extent
    )
    {
        return new(
            imageType: ImageType.Type2D,
            format: format,
            extent: extent,
            mipLevels: 1,
            arrayLayers: 1,
            samples: SampleCountFlags.Count1Bit,
            tiling: ImageTiling.Optimal,
            usage: usageFlags,
            sharingMode: SharingMode.Exclusive,
            initialLayout: ImageLayout.Undefined
        );
    }

    private static unsafe void TransitionLayout(
        Vk api,
        CommandBuffer commandBuffer,
        Image image,
        ImageLayout sourceLayout,
        AccessFlags sourceAccessMask,
        ImageLayout destinationLayout,
        AccessFlags destinationAccessMask,
        uint mipLevels
    )
    {
        var subresourceRange = new ImageSubresourceRange(
            ImageAspectFlags.ColorBit,
            0,
            mipLevels,
            0,
            1
        );

        var barrier = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            SrcAccessMask = sourceAccessMask,
            DstAccessMask = destinationAccessMask,
            OldLayout = sourceLayout,
            NewLayout = destinationLayout,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = image,
            SubresourceRange = subresourceRange,
        };

        api.CmdPipelineBarrier(
            commandBuffer,
            PipelineStageFlags.AllCommandsBit,
            PipelineStageFlags.AllCommandsBit,
            0,
            0,
            null,
            0,
            null,
            1,
            in barrier
        );
    }

    private static unsafe ImageViewCreateInfo GetImageViewCreateInfo(
        Format format,
        Image image,
        ImageAspectFlags aspectFlags
    )
    {
        return new(
            viewType: ImageViewType.Type2D,
            image: image,
            format: format,
            subresourceRange: new(aspectFlags, 0, 1, 0, 1)
        );
    }
}

public sealed class VulkanImageView : SingleNonblockingDisposable<EmptyStruct>
{
    private readonly IVulkanContext _context;
    public ImageView ImageView { get; }

    public VulkanImageView(IVulkanContext context, ref readonly ImageViewCreateInfo info)
        : base(default)
    {
        _context = context;

        _context.Api.CreateImageView(
            _context.Device,
            in info,
            in _context.Callbacks.Handle,
            out var imageView
        );
        ImageView = imageView;
    }

    protected override void Dispose(EmptyStruct context)
    {
        _context.Api.DestroyImageView(_context.Device, ImageView, in _context.Callbacks.Handle);
    }
}
