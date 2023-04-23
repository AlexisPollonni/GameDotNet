using GameDotNet.Core.Tools.Extensions;
using GameDotNet.Graphics.Vulkan.Tools;
using Silk.NET.Vulkan;

namespace GameDotNet.Graphics.Vulkan.Wrappers;

public sealed class VulkanDevice : IDisposable
{
    private readonly AllocationCallbacks? _allocationCallbacks;
    private readonly Device _device;
    private readonly VulkanInstance _instance;
    private readonly VulkanPhysDevice _physDevice;
    private readonly IReadOnlyList<QueueFamilyProperties> _queueFamilies;
    private readonly SurfaceKHR? _surface;

    public VulkanDevice(VulkanInstance instance, VulkanPhysDevice physDevice, Device device, SurfaceKHR? surface,
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
    }

    public static implicit operator Device(VulkanDevice device) => device._device;

    public uint? GetQueueFamilyIndex(QueueType type)
    {
        var index = type switch
        {
            QueueType.Present => QueueTools.GetPresentQueueFamilyIndex(_instance, _physDevice, _surface!.Value,
                                                                 _queueFamilies),
            QueueType.Graphics => QueueTools.GetFirstQueueFamilyIndex(_queueFamilies, QueueFlags.GraphicsBit),
            QueueType.Compute => QueueTools.GetSeparateQueueFamilyIndex(_queueFamilies, QueueFlags.ComputeBit,
                                                                  QueueFlags.TransferBit),
            QueueType.Transfer => QueueTools.GetSeparateQueueFamilyIndex(_queueFamilies, QueueFlags.TransferBit,
                                                                   QueueFlags.ComputeBit),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };

        return (uint?)index;
    }

    public Queue? GetQueue(QueueType type)
    {
        var i = GetQueueFamilyIndex(type);
        if (i is null)
            return null;

        _instance.Vk.GetDeviceQueue(_device, i.Value, 0, out var queue);
        return queue;
    }

    private void ReleaseUnmanagedResources()
    {
        _instance.Vk.DestroyDevice(_device, _allocationCallbacks.AsReadOnlyRefOrNull());
    }
}