using Avalonia;
using GameDotNet.Core.Tooling;
using GameDotNet.Graphics.Vulkan.Abstractions;
using GameDotNet.Graphics.Vulkan.MemoryAllocation;
using GameDotNet.Graphics.Vulkan.Wrappers;
using Nito.Disposables;
using Silk.NET.Vulkan;
using SkiaSharp;

namespace GameDotNet.Editor.Views;

internal class SkiaSwapchainImage : SingleDisposable<EmptyStruct>
{
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
                Memory = Image.Allocation.DeviceMemory.Handle,
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
            CurrentQueueFamily = 0,
        };

    public SkiaSwapchainImage(IVulkanContext context, PixelSize size)
        : base(default)
    {
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
    }

    protected override void Dispose(EmptyStruct context)
    {
        Image.Dispose();
    }
}
