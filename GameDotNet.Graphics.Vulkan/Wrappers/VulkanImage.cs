using GameDotNet.Graphics.Vulkan.MemoryAllocation;
using Silk.NET.Vulkan;

namespace GameDotNet.Graphics.Vulkan.Wrappers;

public class VulkanImage : IDisposable
{
    public Image Image { get; }
    public Allocation Allocation { get; }

    public Format Format => _info.Format;
    public Extent3D Extent => _info.Extent;


    private readonly Vk _vk;
    private readonly VulkanDevice _device;
    private readonly VulkanMemoryAllocator _allocator;

    private ImageCreateInfo _info;
    private AccessFlags _currentAccessFlags;


    public VulkanImage(Vk vk, VulkanDevice device, VulkanMemoryAllocator allocator, in ImageCreateInfo info,
                       in AllocationCreateInfo allocInfo)
    {
        _vk = vk;
        _device = device;
        _allocator = allocator;
        _info = info;

        Image = _allocator.CreateImage(info, allocInfo, out var alloc);
        Allocation = alloc;
    }

    public static implicit operator Image(VulkanImage img) => img.Image;

    public VulkanImageView GetImageView(in ImageViewCreateInfo info) => new(_vk, _device, info);

    public VulkanImageView GetImageView(Format format, ImageAspectFlags aspectFlags) =>
        GetImageView(GetImageViewCreateInfo(format, Image, aspectFlags));


    public void TransitionLayout(CommandBuffer commandBuffer,
                                 ImageLayout fromLayout, AccessFlags fromAccessFlags,
                                 ImageLayout destinationLayout, AccessFlags destinationAccessFlags)
    {
        TransitionLayout(_vk, commandBuffer, Image,
                         fromLayout,
                         fromAccessFlags,
                         destinationLayout, destinationAccessFlags,
                         _info.MipLevels);

        _info.InitialLayout = destinationLayout;

        _currentAccessFlags = destinationAccessFlags;
    }

    public void TransitionLayout(CommandBuffer commandBuffer,
                                 ImageLayout destinationLayout, AccessFlags destinationAccessFlags)
        => TransitionLayout(commandBuffer, _info.InitialLayout, _currentAccessFlags, destinationLayout,
                            destinationAccessFlags);


    public void TransitionLayout(VulkanCommandBufferPool pool, ImageLayout destinationLayout,
                                 AccessFlags destinationAccessFlags)
    {
        var commandBuffer = pool.CreateCommandBuffer();
        commandBuffer.BeginRecording();
        TransitionLayout(commandBuffer.InternalHandle, destinationLayout, destinationAccessFlags);
        commandBuffer.EndRecording();
        commandBuffer.Submit();
    }

    public void TransitionLayout(VulkanCommandBufferPool pool, uint destinationLayout, uint destinationAccessFlags)
    {
        TransitionLayout(pool, (ImageLayout)destinationLayout, (AccessFlags)destinationAccessFlags);
    }

    public void Dispose()
    {
        Allocation.Dispose();

        GC.SuppressFinalize(this);
    }

    public static unsafe ImageCreateInfo GetImageCreateInfo(Format format, ImageUsageFlags usageFlags, Extent3D extent)
    {
        return new(imageType: ImageType.Type2D,
                   format: format,
                   extent: extent,
                   mipLevels: 1,
                   arrayLayers: 1,
                   samples: SampleCountFlags.Count1Bit,
                   tiling: ImageTiling.Optimal,
                   usage: usageFlags);
    }

    private static unsafe void TransitionLayout(
        Vk api,
        CommandBuffer commandBuffer,
        Image image,
        ImageLayout sourceLayout,
        AccessFlags sourceAccessMask,
        ImageLayout destinationLayout,
        AccessFlags destinationAccessMask,
        uint mipLevels)
    {
        var subresourceRange = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, mipLevels, 0, 1);

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
            SubresourceRange = subresourceRange
        };

        api.CmdPipelineBarrier(commandBuffer,
                               PipelineStageFlags.AllCommandsBit,
                               PipelineStageFlags.AllCommandsBit,
                               0,
                               0,
                               null,
                               0,
                               null,
                               1,
                               barrier);
    }

    private static unsafe ImageViewCreateInfo GetImageViewCreateInfo(Format format, Image image,
                                                                     ImageAspectFlags aspectFlags)
    {
        return new(viewType: ImageViewType.Type2D,
                   image: image,
                   format: format,
                   subresourceRange: new(aspectFlags, 0, 1, 0, 1));
    }
}

public class VulkanImageView : IDisposable
{
    private readonly Vk _vk;
    private readonly VulkanDevice _device;
    public ImageView ImageView { get; }

    public unsafe VulkanImageView(Vk vk, VulkanDevice device, in ImageViewCreateInfo info)
    {
        _vk = vk;
        _device = device;

        _vk.CreateImageView(_device, info, null, out var imageView);
        ImageView = imageView;
    }

    public unsafe void Dispose()
    {
        _vk.DestroyImageView(_device, ImageView, null);
    }
}