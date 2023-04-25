using GameDotNet.Graphics.Vulkan.MemoryAllocation;
using Silk.NET.Vulkan;

namespace GameDotNet.Graphics.Vulkan.Wrappers;

public interface IVulkanContext : IDisposable
{
    public Vk Api { get; }
    public VulkanInstance Instance { get; }
    public VulkanPhysDevice PhysDevice { get; }
    public VulkanDevice Device { get; }

    public VulkanMemoryAllocator Allocator { get; }
    public VulkanCommandBufferPool Pool { get; }
}