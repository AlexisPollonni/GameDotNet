using GameDotNet.Core.Tools.Extensions;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace GameDotNet.Core.Graphics.Vulkan.Bootstrap;

public class SwapchainBuilder
{
    private readonly VulkanDevice _device;

    private readonly KhrSwapchain _extension;
    private readonly Info _info;
    private readonly VulkanInstance _instance;
    private readonly VulkanPhysDevice _physDevice;
    private readonly Vk _vk;

    public SwapchainBuilder(VulkanInstance instance, VulkanPhysDevice physDevice, VulkanDevice device,
                            VulkanSurface surface) : this(instance, physDevice, device, new Info(surface))
    { }

    public SwapchainBuilder(VulkanInstance instance, VulkanPhysDevice physDevice, VulkanDevice device, Info info)
    {
        _vk = instance.Vk;
        _instance = instance;
        _physDevice = physDevice;
        _device = device;
        _info = info;

        _info.GraphicsQueueIndex = device.GetQueueIndex(QueueType.Graphics)
                                   ?? throw new ArgumentException("Couldn't find graphics queue of Vulkan device");
        _info.PresentQueueIndex = device.GetQueueIndex(QueueType.Present)
                                  ?? throw new ArgumentException("Couldn't find present queue of Vulkan device");

        if (!instance.Vk.TryGetDeviceExtension(instance, _device, out _extension))
        {
            throw new
                InvalidOperationException("Can't create Vulkan Swapchain, VK_KHR_swapchain extension not available");
        }
    }

    public VulkanSwapchain Build()
    {
        if (_info.Surface.AsSurfaceKhr().Handle is 0)
            throw new ArgumentException("Surface is not initialized", nameof(_info.Surface));

        var desiredFormats = _info.DesiredFormats;
        if (desiredFormats.Count is 0) desiredFormats = Info.DefaultFormats.ToList();
        var desiredPresentModes = _info.DesiredPresentModes;
        if (desiredPresentModes.Count is 0) desiredPresentModes = Info.DefaultPresentModes.ToList();

        var surfaceSupport = QuerySurfaceSupportDetails(_physDevice, _info.Surface);

        var imageCount = surfaceSupport.Capabilities.MinImageCount + 1;
        if (surfaceSupport.Capabilities.MaxImageCount > 0 && imageCount > surfaceSupport.Capabilities.MaxImageCount)
        {
            imageCount = surfaceSupport.Capabilities.MaxImageCount;
        }

        var surfaceFormat = FindSurfaceFormat(_physDevice, _info.DesiredFormats, surfaceSupport.Formats,
                                              _info.FormatFeatureFlags);

        var extent = FindExtent(surfaceSupport.Capabilities, _info.DesiredWidth, _info.DesiredHeight);

        var imageArrayLayers = _info.ArrayLayerCount;
        if (surfaceSupport.Capabilities.MaxImageArrayLayers < _info.ArrayLayerCount)
            imageArrayLayers = surfaceSupport.Capabilities.MaxImageArrayLayers;
        if (_info.ArrayLayerCount is 0)
            imageArrayLayers = 1;

        var queueFamilyIndices = new[] { _info.GraphicsQueueIndex, _info.PresentQueueIndex };
        var presentMode = FindPresentMode(surfaceSupport.PresentModes, _info.DesiredPresentModes);

        var preTransform = _info.PreTransform;
        if (preTransform is 0)
            preTransform = surfaceSupport.Capabilities.CurrentTransform;

        var swapchainCreateInfo = new SwapchainCreateInfoKHR
        {
            SType = StructureType.SwapchainCreateInfoKhr,
            Flags = _info.CreateFlags,
            Surface = _info.Surface,
            MinImageCount = imageCount,
            ImageFormat = surfaceFormat.Format,
            ImageColorSpace = surfaceFormat.ColorSpace,
            ImageExtent = extent,
            ImageArrayLayers = imageArrayLayers,
            ImageUsage = _info.ImageUsageFlags,
            PreTransform = preTransform,
            PresentMode = presentMode,
            Clipped = _info.Clipped
        };
        if (_info.OldSwapchain is not null) swapchainCreateInfo.OldSwapchain = _info.OldSwapchain;
        if (_info.GraphicsQueueIndex != _info.PresentQueueIndex)
        {
            unsafe
            {
                swapchainCreateInfo.ImageSharingMode = SharingMode.Concurrent;
                swapchainCreateInfo.QueueFamilyIndexCount = 2;
                swapchainCreateInfo.PQueueFamilyIndices = queueFamilyIndices.ToGlobalMemory().AsPtr<uint>();
            }
        }
        else swapchainCreateInfo.ImageSharingMode = SharingMode.Exclusive;

        SwapchainKHR swapchain;
        unsafe
        {
            var res = _extension.CreateSwapchain(_device, swapchainCreateInfo, null, out swapchain);

            if (res is not Result.Success) throw new VulkanException(res);
        }

        return new(_instance, _device, swapchain)
        {
            Extent = extent, ImageFormat = surfaceFormat.Format
        };
    }

    private static SurfaceSupportDetails QuerySurfaceSupportDetails(VulkanPhysDevice device, VulkanSurface surface)
    {
        var capabilities = surface.GetCapabilities(device);
        var formats = surface.GetSurfaceFormats(device);
        var presentModes = surface.GetPresentModes(device);

        return new(capabilities, formats, presentModes);
    }

    private SurfaceFormatKHR FindSurfaceFormat(VulkanPhysDevice device,
                                               IReadOnlyList<SurfaceFormatKHR> availableFormats,
                                               IReadOnlyList<SurfaceFormatKHR> desiredFormats,
                                               FormatFeatureFlags featureFlags)
    {
        foreach (var desired in desiredFormats)
        {
            foreach (var available in availableFormats)
            {
                //Finds the first format that is desired or available
                if (desired.Format != available.Format || desired.ColorSpace != available.ColorSpace)
                    continue;

                _vk.GetPhysicalDeviceFormatProperties(device, desired.Format, out var properties);
                if (properties.OptimalTilingFeatures.HasFlag(featureFlags))
                {
                    return desired;
                }
            }
        }

        // use the first available one if any desired formats aren't found
        return availableFormats[0];
    }

    private static Extent2D FindExtent(SurfaceCapabilitiesKHR capabilities, uint desiredWidth, uint desiredHeight)
    {
        if (capabilities.CurrentExtent.Width != uint.MaxValue)
            return capabilities.CurrentExtent;

        return new()
        {
            Width = Math.Max(capabilities.MinImageExtent.Width,
                             Math.Min(capabilities.MaxImageExtent.Width, desiredWidth)),
            Height = Math.Max(capabilities.MinImageExtent.Height,
                              Math.Min(capabilities.MaxImageExtent.Height, desiredHeight))
        };
    }

    private static PresentModeKHR FindPresentMode(IReadOnlyList<PresentModeKHR> availablePresentModes,
                                                  IReadOnlyList<PresentModeKHR> desiredPresentModes)
    {
        foreach (var desired in desiredPresentModes)
        {
            if (availablePresentModes.Any(available => desired == available))
            {
                return desired;
            }
        }

        // only preset mode required, use as fallback
        return PresentModeKHR.FifoKhr;
    }

    private class SurfaceSupportDetails
    {
        public readonly SurfaceCapabilitiesKHR Capabilities;
        public readonly IReadOnlyList<SurfaceFormatKHR> Formats;
        public readonly IReadOnlyList<PresentModeKHR> PresentModes;

        public SurfaceSupportDetails(SurfaceCapabilitiesKHR capabilities, IReadOnlyList<SurfaceFormatKHR> formats,
                                     IReadOnlyList<PresentModeKHR> presentModes)
        {
            Capabilities = capabilities;
            Formats = formats;
            PresentModes = presentModes;
        }
    }

    public class Info
    {
        public bool Clipped = true;
        public uint ArrayLayerCount = 1;
        public CompositeAlphaFlagsKHR CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr;
        public SwapchainCreateFlagsKHR CreateFlags = 0;

        public List<SurfaceFormatKHR> DesiredFormats = DefaultFormats.ToList();
        public uint DesiredHeight = 256;
        public List<PresentModeKHR> DesiredPresentModes = DefaultPresentModes.ToList();


        public uint DesiredWidth = 256;
        public FormatFeatureFlags FormatFeatureFlags = FormatFeatureFlags.SampledImageBit;
        public uint GraphicsQueueIndex;
        public ImageUsageFlags ImageUsageFlags = ImageUsageFlags.ColorAttachmentBit;

        public VulkanSwapchain? OldSwapchain;
        public uint PresentQueueIndex;
        public SurfaceTransformFlagsKHR PreTransform = 0;
        public VulkanSurface Surface;

        public Info(VulkanSurface surface)
        {
            Surface = surface;
        }

        public static IEnumerable<SurfaceFormatKHR> DefaultFormats { get; } = new SurfaceFormatKHR[]
        {
            new(Format.B8G8R8A8Srgb, ColorSpaceKHR.PaceSrgbNonlinearKhr),
            new(Format.R8G8B8A8Srgb, ColorSpaceKHR.PaceSrgbNonlinearKhr)
        };

        public static IEnumerable<PresentModeKHR> DefaultPresentModes { get; } = new[]
        {
            PresentModeKHR.MailboxKhr, PresentModeKHR.FifoKhr
        };
    }
}