using Silk.NET.Vulkan;

namespace Core.Graphics.Vulkan;

public class VulkanDevice : IDisposable
{
    private readonly AllocationCallbacks? _allocationCallbacks;
    private readonly Device _device;
    private readonly VulkanPhysDevice _physDevice;
    private readonly IReadOnlyList<QueueFamilyProperties> _queueFamilies;
    private readonly SurfaceKHR _surface;
    private readonly Vk _vk;

    public VulkanDevice(Vk vk, Device device, VulkanPhysDevice physDevice, SurfaceKHR surface,
                        IReadOnlyList<QueueFamilyProperties> queueFamilies, AllocationCallbacks? allocationCallbacks)
    {
        _vk = vk;
        _device = device;
        _physDevice = physDevice;
        _surface = surface;
        _queueFamilies = queueFamilies;
        _allocationCallbacks = allocationCallbacks;
    }

    public unsafe void Dispose()
    {
        if (_allocationCallbacks is null)
            _vk.DestroyDevice(_device, null);
        else
            _vk.DestroyDevice(_device, _allocationCallbacks.Value);

        GC.SuppressFinalize(this);
    }

    public static implicit operator Device(VulkanDevice device) => device._device;
}