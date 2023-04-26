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
    internal IReadOnlyList<string> ExtensionsToEnable { get; init; } = Array.Empty<string>();
    internal IReadOnlyList<QueueFamilyProperties> QueueFamilies { get; init; } = Array.Empty<QueueFamilyProperties>();

    internal IReadOnlyList<GenericFeaturesNextNode> ExtendedFeaturesChain { get; init; } =
        Array.Empty<GenericFeaturesNextNode>();

    internal bool DeferSurfaceInit { get; init; }
}