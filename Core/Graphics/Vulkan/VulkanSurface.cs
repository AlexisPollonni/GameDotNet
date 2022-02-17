using Core.Tools.Extensions;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace Core.Graphics.Vulkan;

public sealed class VulkanSurface : IDisposable
{
    private readonly VulkanInstance _instance;
    private readonly SurfaceKHR _surface;
    private readonly KhrSurface _surfaceExt;

    internal VulkanSurface(VulkanInstance instance, SurfaceKHR surface)
    {
        _surface = surface;
        _instance = instance;

        if (!_instance.Vk.TryGetInstanceExtension(instance, out _surfaceExt))
            throw new
                InvalidOperationException("Can't create Vulkan Surface, VK_KHR_Surface instance extension not available");
    }

    public void Dispose()
    {
        ReleaseUnmanagedResources();
        GC.SuppressFinalize(this);
    }

    public static implicit operator SurfaceKHR(VulkanSurface surface) => surface.AsSurfaceKhr();

    internal SurfaceKHR AsSurfaceKhr() => _surface;

    public SurfaceCapabilitiesKHR GetCapabilities(VulkanPhysDevice device)
    {
        var res = _surfaceExt.GetPhysicalDeviceSurfaceCapabilities(device, _surface, out var capabilities);
        if (res != Result.Success)
            throw new VulkanException(res);

        return capabilities;
    }

    public IReadOnlyList<SurfaceFormatKHR> GetSurfaceFormats(VulkanPhysDevice device)
    {
        var count = 0U;
        _surfaceExt.GetPhysicalDeviceSurfaceFormats(device, _surface, count.ToSpan(), Span<SurfaceFormatKHR>.Empty);
        var formats = new SurfaceFormatKHR[count];
        _surfaceExt.GetPhysicalDeviceSurfaceFormats(device, _surface, count.ToSpan(), formats);

        return formats;
    }

    public IReadOnlyList<PresentModeKHR> GetPresentModes(VulkanPhysDevice device)
    {
        var count = 0U;
        _surfaceExt.GetPhysicalDeviceSurfacePresentModes(device, _surface, count.ToSpan(), Span<PresentModeKHR>.Empty);
        var modes = new PresentModeKHR[count];
        _surfaceExt.GetPhysicalDeviceSurfacePresentModes(device, _surface, count.ToSpan(), modes);

        return modes;
    }

    ~VulkanSurface()
    {
        ReleaseUnmanagedResources();
    }

    private unsafe void ReleaseUnmanagedResources()
    {
        _surfaceExt.DestroySurface(_instance, _surface, null);
    }
}