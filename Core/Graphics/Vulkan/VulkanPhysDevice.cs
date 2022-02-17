using Core.Graphics.Vulkan.Bootstrap;
using Silk.NET.Core;
using Silk.NET.Vulkan;

namespace Core.Graphics.Vulkan;

public sealed class VulkanPhysDevice
{
    //My kingdom for C#11 required !!
    public PhysicalDevice Device { get; init; }
    public SurfaceKHR Surface { get; init; }

    public PhysicalDeviceFeatures Features { get; init; }
    public PhysicalDeviceProperties Properties { get; init; }
    public PhysicalDeviceMemoryProperties MemoryProperties { get; init; }

    internal Version32 InstanceVersion { get; init; }
    internal IReadOnlyList<string> ExtensionsToEnable { get; init; } = Array.Empty<string>();
    internal IReadOnlyList<QueueFamilyProperties> QueueFamilies { get; init; } = Array.Empty<QueueFamilyProperties>();

    internal IReadOnlyList<GenericFeaturesNextNode> ExtendedFeaturesChain { get; init; } =
        Array.Empty<GenericFeaturesNextNode>();

    internal bool DeferSurfaceInit { get; init; }


    public static implicit operator PhysicalDevice(VulkanPhysDevice device) => device.Device;
}