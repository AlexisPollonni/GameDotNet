using Core.Tools.Extensions;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace Core.Graphics.Vulkan;

public sealed class VulkanSwapchain : IDisposable
{
    private readonly AllocationCallbacks? _alloc;
    private readonly VulkanDevice _device;
    private readonly KhrSwapchain _extension;

    private readonly VulkanInstance _instance;

    public IReadOnlyList<Image> GetImages()
    {
        if (_images is not null && _images.Length == ImageCount)
            return _images;

        var count = 0U;
        _extension.GetSwapchainImages(_device, Swapchain, count.AsSpan(), Span<Image>.Empty);
        var images = new Image[count];
        _extension.GetSwapchainImages(_device, Swapchain, count.AsSpan(), images);

        return _images = images;
    }

    public uint ImageCount { get; }
    public Format ImageFormat { get; init; } = Format.Undefined;
    public Extent2D Extent { get; init; } = new(0, 0);

    public unsafe void Dispose()
    {
        if (_alloc is null)
            _extension.DestroySwapchain(_device, _swapchain, null);
        else
            _extension.DestroySwapchain(_device, _swapchain, _alloc.Value);
    }

    public static implicit operator SwapchainKHR(VulkanSwapchain swapchain) => swapchain._swapchain;

    public IReadOnlyList<Image> GetImages()
    {
        var count = 0U;
        _extension.GetSwapchainImages(_device, _swapchain, count.ToSpan(), Span<Image>.Empty);
        var images = new Image[count];
        _extension.GetSwapchainImages(_device, _swapchain, count.ToSpan(), images);

        return images;
    }
}