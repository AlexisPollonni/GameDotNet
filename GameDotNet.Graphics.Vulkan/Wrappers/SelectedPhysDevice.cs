using GameDotNet.Graphics.Vulkan.Bootstrap;
using Silk.NET.Core;
using Silk.NET.Vulkan;

namespace GameDotNet.Graphics.Vulkan.Wrappers;

public sealed class SelectedPhysDevice
{
    public required VulkanPhysDevice Device { get; init; }
    public VulkanSurface? Surface { get; init; }

    public PhysicalDeviceFeatures Features { get; init; }
    public PhysicalDeviceProperties Properties { get; init; }
    public PhysicalDeviceMemoryProperties MemoryProperties { get; init; }

    internal Version32 InstanceVersion { get; init; }
    internal IReadOnlyList<string> ExtensionsToEnable { get; init; } = [];
    internal IReadOnlyList<QueueFamilyProperties> QueueFamilies { get; init; } = [];

    internal IReadOnlyList<GenericFeaturesNextNode> ExtendedFeaturesChain { get; init; } = [];

    internal bool DeferSurfaceInit { get; init; }
}
