using GameDotNet.Graphics.Vulkan.Wrappers;

namespace GameDotNet.Graphics.Vulkan.Abstractions;

public interface IVulkanWrapper<out TUnderlying>
    where TUnderlying : unmanaged
{
    internal IVulkanContext Context { get; }
    internal TUnderlying Underlying { get; }
}
