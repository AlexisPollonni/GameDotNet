using GameDotNet.Core.Graphics.MemoryAllocation;
using Silk.NET.Vulkan;

namespace GameDotNet.Core.Graphics.Vulkan;

public class VulkanImage : IDisposable
{
    public Image Image { get; }
    public Allocation Allocation { get; }

    private readonly Vk _vk;
    private readonly VulkanDevice _device;
    private readonly VulkanMemoryAllocator _allocator;


    public VulkanImage(Vk vk, VulkanDevice device, VulkanMemoryAllocator allocator, in ImageCreateInfo info,
                       in AllocationCreateInfo allocInfo)
    {
        _vk = vk;
        _device = device;
        _allocator = allocator;

        Image = _allocator.CreateImage(info, allocInfo, out var alloc);
        Allocation = alloc;
    }

    public static implicit operator Image(VulkanImage img) => img.Image;

    public VulkanImageView GetImageView(in ImageViewCreateInfo info) => new(_vk, _device, info);

    public VulkanImageView GetImageView(Format format, Image image, ImageAspectFlags aspectFlags) =>
        GetImageView(GetImageViewCreateInfo(format, image, aspectFlags));

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