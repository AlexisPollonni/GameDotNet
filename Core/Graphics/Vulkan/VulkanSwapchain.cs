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

    private readonly SwapchainKHR _swapchain;

    internal VulkanSwapchain(VulkanInstance instance, VulkanDevice device, SwapchainKHR swapchain,
                             AllocationCallbacks? alloc = null)
    {
        _instance = instance;
        _device = device;
        _swapchain = swapchain;
        _alloc = alloc;

        if (!instance.Vk.TryGetDeviceExtension(_instance, _device, out _extension))
        {
            throw new
                InvalidOperationException("Can't create Vulkan Swapchain, VK_KHR_swapchain device extension not available");
        }

        ImageCount = (uint)GetImages().Count;
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