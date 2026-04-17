using GameDotNet.Graphics.Vulkan.Wrappers;

namespace GameDotNet.Graphics.Vulkan.Abstractions;

//TODO: performance improvements: consider turning wrappers into generic structs, move wrapper logic to extension methods of IVulkanWrapper<TUnderlying> and add global aliases for each wrapper such as VulkanWrapper<Device> = VulkanDevice
public interface IVulkanWrapper<out TUnderlying>
    where TUnderlying : unmanaged
{
    internal IVulkanContext Context { get; }
    internal TUnderlying Underlying { get; }
}
