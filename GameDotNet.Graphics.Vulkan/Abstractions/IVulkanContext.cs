using GameDotNet.Graphics.Vulkan.MemoryAllocation;
using GameDotNet.Graphics.Vulkan.Services;
using GameDotNet.Graphics.Vulkan.Tools.Allocators;
using GameDotNet.Graphics.Vulkan.Wrappers;
using Silk.NET.Vulkan;

namespace GameDotNet.Graphics.Vulkan.Abstractions;

public interface IVulkanContext : IDisposable
{
    public Vk Api { get; }
    public VulkanInstance Instance { get; }
    public IVulkanAllocCallback Callbacks { get; }
    public SelectedPhysDevice PhysDevice { get; }
    public VulkanDevice Device { get; }
    public DeviceQueuesManager Queues { get; }
    public VulkanMemoryAllocator Allocator { get; }
}
