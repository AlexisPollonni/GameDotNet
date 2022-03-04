using GameDotNet.Core.Tools.Extensions;
using Silk.NET.Vulkan;

namespace GameDotNet.Core.Graphics.Vulkan;

public sealed class VulkanDevice : IDisposable
{
    private readonly AllocationCallbacks? _allocationCallbacks;
    private readonly Device _device;
    private readonly VulkanInstance _instance;
    private readonly VulkanPhysDevice _physDevice;
    private readonly IReadOnlyList<QueueFamilyProperties> _queueFamilies;
    private readonly SurfaceKHR _surface;

    internal VulkanDevice(VulkanInstance instance, VulkanPhysDevice physDevice, Device device, SurfaceKHR surface,
                          IReadOnlyList<QueueFamilyProperties> queueFamilies, AllocationCallbacks? allocationCallbacks)
    {
        _instance = instance;
        _physDevice = physDevice;
        _device = device;
        _surface = surface;
        _queueFamilies = queueFamilies;
        _allocationCallbacks = allocationCallbacks;
    }

    public void Dispose()
    {
        ReleaseUnmanagedResources();
        GC.SuppressFinalize(this);
    }

    public static implicit operator Device(VulkanDevice device) => device._device;

    public uint? GetQueueIndex(QueueType type)
    {
        var index = type switch
        {
            QueueType.Present => QueueTools.GetPresentQueueIndex(_instance, _physDevice, _surface, _queueFamilies),
            QueueType.Graphics => QueueTools.GetFirstQueueIndex(_queueFamilies, QueueFlags.QueueGraphicsBit),
            QueueType.Compute => QueueTools.GetSeparateQueueIndex(_queueFamilies, QueueFlags.QueueComputeBit,
                                                                  QueueFlags.QueueTransferBit),
            QueueType.Transfer => QueueTools.GetSeparateQueueIndex(_queueFamilies, QueueFlags.QueueTransferBit,
                                                                   QueueFlags.QueueComputeBit),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };

        return (uint?)index;
    }

    public Queue? GetQueue(QueueType type)
    {
        var i = GetQueueIndex(type);
        if (i is null)
            return null;

        _instance.Vk.GetDeviceQueue(_device, i.Value, 0, out var queue);
        return queue;
    }

    ~VulkanDevice() => ReleaseUnmanagedResources();

    private void ReleaseUnmanagedResources()
    {
        _instance.Vk.DestroyDevice(_device, _allocationCallbacks.AsReadOnlyRefOrNull());
    }
}