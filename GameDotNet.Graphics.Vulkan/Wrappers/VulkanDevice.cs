using GameDotNet.Core.Tools.Extensions;
using Silk.NET.Vulkan;

namespace GameDotNet.Graphics.Vulkan.Wrappers;

public sealed class VulkanDevice : IDisposable
{
    public DeviceQueuesManager QueuesManager { get; }

    private readonly Device _device;
    private readonly VulkanInstance _instance;
    private readonly AllocationCallbacks? _allocationCallbacks;

    public VulkanDevice(VulkanInstance instance, VulkanPhysDevice physDevice, Device device,
                        AllocationCallbacks? allocationCallbacks)
    {
        QueuesManager = new(instance, physDevice, this);

        _instance = instance;
        _device = device;
        _allocationCallbacks = allocationCallbacks;
    }

    public void Dispose()
    {
        ReleaseUnmanagedResources();
    }

    public static implicit operator Device(VulkanDevice device) => device._device;

    private void ReleaseUnmanagedResources()
    {
        _instance.Vk.DestroyDevice(_device, _allocationCallbacks.AsReadOnlyRefOrNull());
    }
}