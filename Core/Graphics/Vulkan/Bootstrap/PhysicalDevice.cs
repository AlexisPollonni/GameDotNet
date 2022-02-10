using Silk.NET.Core;
using Silk.NET.Vulkan;

namespace Core.Graphics.Vulkan.Bootstrap;

public class VulkanPhysDevice
{
    public PhysicalDevice Device { get; init; }
    public SurfaceKHR Surface { get; init; }

    public PhysicalDeviceFeatures Features { get; init; }
    public PhysicalDeviceProperties Properties { get; init; }
    public PhysicalDeviceMemoryProperties MemoryProperties { get; init; }

    internal Version32 InstanceVersion { get; init; }
    internal IReadOnlyList<string> ExtensionsToEnable { get; init; } = null!; //My kingdom for C#11 required !!
    internal IReadOnlyList<QueueFamilyProperties> QueueFamilies { get; init; } = null!;
    internal bool DeferSurfaceInit { get; init; }


    public static implicit operator PhysicalDevice(VulkanPhysDevice device) => device.Device;
}