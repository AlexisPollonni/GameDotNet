using GameDotNet.Core.Tooling.Extensions;
using GameDotNet.Graphics.Vulkan.Tools;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace GameDotNet.Graphics.Vulkan.Wrappers;

public sealed class VulkanSwapchain : IDisposable
{
    private readonly AllocationCallbacks? _alloc;
    private readonly VulkanDevice _device;
    private readonly KhrSwapchain _extension;
    private readonly VulkanInstance _instance;

    internal readonly SwapchainKHR Swapchain;

    private Image[]? _images;
    private ImageView[]? _imageViews;

    internal VulkanSwapchain(
        VulkanInstance instance,
        VulkanDevice device,
        SwapchainKHR swapchain,
        AllocationCallbacks? alloc = null
    )
    {
        _instance = instance;
        _device = device;
        Swapchain = swapchain;
        _alloc = alloc;

        if (!instance.Context.Api.TryGetDeviceExtension(_instance, _device, out _extension))
        {
            throw new InvalidOperationException(
                "Can't create Vulkan Swapchain, VK_KHR_swapchain device extension not available"
            );
        }

        ImageCount = (uint)GetImages().Count;
    }

    public uint ImageCount { get; }
    public Format ImageFormat { get; init; } = Format.Undefined;
    public Extent2D Extent { get; init; } = new(0, 0);

    public void Dispose()
    {
        if (_imageViews != null)
        {
            foreach (var view in _imageViews)
            {
                _instance.Context.Api.DestroyImageView(
                    _device,
                    view,
                    in _alloc.AsReadOnlyRefOrNull()
                );
            }
        }

        _extension.DestroySwapchain(_device, Swapchain, in _alloc.AsReadOnlyRefOrNull());
    }

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

    public IReadOnlyList<ImageView> GetImageViews()
    {
        if (_imageViews is not null && _imageViews.Length == ImageCount)
            return _imageViews;

        var images = GetImages();
        var imageViews = new ImageView[ImageCount];

        foreach (var (i, image) in images.Index())
        {
            var createInfo = new ImageViewCreateInfo
            {
                SType = StructureType.ImageViewCreateInfo,
                Image = image,
                ViewType = ImageViewType.Type2D,
                Format = ImageFormat,
                Components = new(
                    ComponentSwizzle.Identity,
                    ComponentSwizzle.Identity,
                    ComponentSwizzle.Identity,
                    ComponentSwizzle.Identity
                ),
                SubresourceRange = new(ImageAspectFlags.ColorBit, 0, 1, 0, 1),
            };

            var res = _instance.Context.Api.CreateImageView(
                _device,
                in createInfo,
                in _alloc.AsReadOnlyRefOrNull(),
                out var imageView
            );
            if (res is not Result.Success)
                throw new VulkanException(res);

            imageViews[i] = imageView;
        }

        return _imageViews = imageViews;
    }

    public Result AcquireNextImage(
        ulong timeout,
        Semaphore? semaphore,
        Fence? fence,
        out uint imageIndex
    )
    {
        imageIndex = 0U;
        return _extension.AcquireNextImage(
            _device,
            Swapchain,
            timeout,
            semaphore ?? new(),
            fence ?? new(),
            ref imageIndex
        );
    }

    public Result QueuePresent(DeviceQueue queue, VulkanSemaphore waitSemaphore, uint currentIndex)
    {
        ReadOnlySpan<Semaphore> w = [waitSemaphore.Underlying];
        ReadOnlySpan<SwapchainKHR> s = [Swapchain];

        return QueuePresent(queue, w, s, currentIndex.AsSpan(), null);
    }

    private unsafe Result QueuePresent(
        DeviceQueue queue,
        ReadOnlySpan<Semaphore> waitSemaphores,
        ReadOnlySpan<SwapchainKHR> swapchains,
        ReadOnlySpan<uint> imageIndices,
        Span<Result> results
    )
    {
        Result res;

        fixed (SwapchainKHR* pSwap = swapchains)
        fixed (uint* pIndices = imageIndices)
        fixed (Result* pResults = results)
        fixed (Semaphore* pWaits = waitSemaphores)
        {
            var info = new PresentInfoKHR
            {
                SType = StructureType.PresentInfoKhr,
                SwapchainCount = (uint)swapchains.Length,
                PSwapchains = pSwap,
                PImageIndices = pIndices,
                PResults = pResults,
                WaitSemaphoreCount = (uint)waitSemaphores.Length,
                PWaitSemaphores = pWaits,
            };

            res = queue.Present(_extension, in info);
        }

        return res;
    }

    public static implicit operator SwapchainKHR(VulkanSwapchain swapchain) => swapchain.Swapchain;
}
