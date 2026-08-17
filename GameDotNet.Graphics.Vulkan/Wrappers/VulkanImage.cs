using System.Drawing;
using ByteSizeLib;
using GameDotNet.Core.Tooling;
using GameDotNet.Graphics.Abstractions;
using GameDotNet.Graphics.Vulkan.Abstractions;
using GameDotNet.Graphics.Vulkan.MemoryAllocation;
using GameDotNet.Graphics.Vulkan.Tools.Extensions;
using Nito.Disposables;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace GameDotNet.Graphics.Vulkan.Wrappers;

public interface IVulkanImage : IVulkanWrapper<Image>, IVulkanCreateFromInfo<ImageCreateInfo>
{
    Format Format { get; }
    Extent3D Extent { get; }
    AccessFlags CurrentAccessFlags { get; internal set; }
    ImageLayout CurrentLayout { get; internal set; }
    ByteSize Size { get; }
}

public abstract class VulkanImage : SingleDisposable<EmptyStruct>, IDeviceTexture, IVulkanImage
{
    public IVulkanContext Context { get; }

    public Image Underlying => _lazyImage.Value.Item3;
    public IChain<ImageCreateInfo> InfoChain => _lazyImage.Value.Item1;
    public Allocation Allocation => _lazyImage.Value.Item2;

    public Format Format => this.CreateInfo.Format;
    public Extent3D Extent => this.CreateInfo.Extent;

    public AccessFlags CurrentAccessFlags { get; set; }
    public ImageLayout CurrentLayout { get; set; }
    public ByteSize Size => ByteSize.FromBytes(Allocation.Size);

    public TextureDescription Description =>
        new()
        {
            PixelSize = new((int)Extent.Width, (int)Extent.Height),
            Size = Size,
            SampleCount = this.CreateInfo.Samples switch
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

    public virtual AllocationCreateInfo CreateAllocInfo() => new(usage: MemoryUsage.GPU_Only);

    public abstract IChain<ImageCreateInfo> CreateVulkanInfo();

    private readonly Lazy<(IChain<ImageCreateInfo>, Allocation, Image)> _lazyImage;

    protected VulkanImage(IVulkanContext context)
        : base(default)
    {
        Context = context;

        _lazyImage = new(ImageFactory, false);
    }

    private (IChain<ImageCreateInfo>, Allocation, Image) ImageFactory()
    {
        var allocInfo = CreateAllocInfo();
        var infoChain = CreateVulkanInfo();

        var image = Context.Allocator.CreateImage(
            in infoChain.HeadRef,
            in allocInfo,
            out var alloc
        );

        return (infoChain, alloc, image);
    }

    public static implicit operator Image(VulkanImage img) => img.Underlying;

    public VulkanImageView GetImageView(ref readonly ImageViewCreateInfo info) =>
        new(Context, in info);

    public VulkanImageView GetImageView(Format format, ImageAspectFlags aspectFlags)
    {
        var createInfo = GetImageViewCreateInfo(format, Underlying, aspectFlags);

        return GetImageView(in createInfo);
    }

    protected override void Dispose(EmptyStruct context)
    {
        if (!_lazyImage.IsValueCreated)
            return;
        Allocation.Dispose();
        Context.Api.DestroyImage(Context.Device, Underlying, in Context.Callbacks.Underlying);
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

public sealed class Vulkan2DImage(
    IVulkanContext context,
    Format format,
    Size size,
    ImageUsageFlags usageFlags = ImageUsageFlags.ColorAttachmentBit
) : VulkanImage(context)
{
    public override unsafe IChain<ImageCreateInfo> CreateVulkanInfo() =>
        Chain.Create<ImageCreateInfo>(
            new(
                imageType: ImageType.Type2D,
                format: format,
                extent: new((uint)size.Width, (uint)size.Height, 1),
                mipLevels: 1,
                arrayLayers: 1,
                samples: SampleCountFlags.Count1Bit,
                tiling: ImageTiling.Optimal,
                usage: usageFlags,
                sharingMode: SharingMode.Exclusive,
                initialLayout: ImageLayout.Undefined
            )
        );
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
            in _context.Callbacks.Underlying,
            out var imageView
        );
        ImageView = imageView;
    }

    protected override void Dispose(EmptyStruct context)
    {
        _context.Api.DestroyImageView(_context.Device, ImageView, in _context.Callbacks.Underlying);
    }
}
