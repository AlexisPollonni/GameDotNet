using Avalonia;
using GameDotNet.Core.Tooling;
using GameDotNet.Graphics.Vulkan.Abstractions;
using GameDotNet.Graphics.Vulkan.MemoryAllocation;
using GameDotNet.Graphics.Vulkan.Tools.Extensions;
using GameDotNet.Graphics.Vulkan.Wrappers;
using Nito.Disposables;
using Silk.NET.Vulkan;
using SkiaSharp;

namespace GameDotNet.Editor.Views;

internal class SkiaSwapchainImage(IVulkanContext context, PixelSize size)
    : SingleDisposable<EmptyStruct>(default)
{
    internal VulkanImage Image { get; } =
        new Vulkan2DImage(
            context,
            Format.B8G8R8A8Unorm,
            new(size.Width, size.Height),
            ImageUsageFlags.ColorAttachmentBit
                | ImageUsageFlags.TransferSrcBit
                | ImageUsageFlags.TransferDstBit
                | ImageUsageFlags.SampledBit
        );

    public PixelSize Size { get; } = size;

    public GRVkImageInfo ImageInfo =>
        new()
        {
            Alloc = new()
            {
                Offset = (ulong)Image.Allocation.Offset,
                Size = (ulong)Image.Allocation.Size,
                Memory = Image.Allocation.DeviceMemory.Handle,
            },
            Image = Image.Underlying.Handle,
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

    protected override void Dispose(EmptyStruct context)
    {
        Image.Dispose();
    }
}
