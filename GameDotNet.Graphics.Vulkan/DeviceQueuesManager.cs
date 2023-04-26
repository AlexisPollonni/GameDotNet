using System.Collections.Immutable;
using GameDotNet.Graphics.Vulkan.Tools;
using GameDotNet.Graphics.Vulkan.Wrappers;
using Silk.NET.Vulkan;

namespace GameDotNet.Graphics.Vulkan;

public class DeviceQueuesManager
{
    private readonly Vk _api;
    private readonly VulkanInstance _instance;
    private readonly VulkanPhysDevice _physDevice;
    private readonly VulkanDevice _device;

    private readonly object _lock;

    private readonly ImmutableArray<QueueFamilyProperties> _familyProperties;
    private readonly ImmutableArray<QueueFamilyProperties2> _familyProperties2;
    private readonly DeviceQueue?[][] _queueCache;

    public DeviceQueuesManager(VulkanInstance instance, VulkanPhysDevice physDevice, VulkanDevice device)
    {
        _api = instance.Vk;
        _instance = instance;
        _physDevice = physDevice;
        _device = device;
        _lock = new();

        _familyProperties2 = physDevice.GetQueueFamilyProperties2().ToImmutableArray();
        _familyProperties = _familyProperties2.Select(p => p.QueueFamilyProperties).ToImmutableArray();

        _queueCache = new DeviceQueue[_familyProperties.Length][];

        for (var i = 0; i < _familyProperties.Length; i++)
        {
            _queueCache[i] = new DeviceQueue?[_familyProperties[i].QueueCount];
        }
    }

    public DeviceQueue? GetFirstGraphic()
    {
        return GetFirstQueue(QueueFlags.GraphicsBit);
    }

    public DeviceQueue? GetFirstPresent(VulkanSurface surface)
    {
        var presentFamily = QueueTools.GetPresentQueueFamilyIndex(_instance, _physDevice, surface, _familyProperties);

        return GetFirstQueueFromIndex(presentFamily);
    }

    public DeviceQueue? GetFirstQueue(QueueFlags desiredFlags)
    {
        var index = QueueTools.GetFirstQueueFamilyIndex(_familyProperties, desiredFlags);

        return GetQueueByIndexes(index, 0);
    }

    public DeviceQueue? GetFirstQueueFromIndex(int? familyIndex)
    {
        return GetQueueByIndexes(familyIndex, 0);
    }

    public DeviceQueue? GetDedicatedQueue(QueueFlags desiredFlags, QueueFlags undesiredFlags, bool forceNew = false)
    {
        var index = QueueTools.GetDedicatedQueueFamilyIndex(_familyProperties, desiredFlags, undesiredFlags);

        return GetLastQueueOrNew(index, forceNew);
    }

    public DeviceQueue? GetSeparateQueue(QueueFlags desiredFlags, QueueFlags undesiredFlags, bool forceNew = false)
    {
        var index = QueueTools.GetSeparateQueueFamilyIndex(_familyProperties, desiredFlags, undesiredFlags);

        return GetLastQueueOrNew(index, forceNew);
    }

    private DeviceQueue? GetLastQueueOrNew(int? familyIndex, bool forceNew)
    {
        if (familyIndex is null) return null;

        var queues = _queueCache[familyIndex.Value];

        if (forceNew)
        {
            var i = Array.IndexOf(queues, null);
            return i < 0 ? null : GetQueueByIndexes(familyIndex, i);
        }
        else
        {
            var i = Array.FindLastIndex(queues, queue => queue is not null);
            return GetQueueByIndexes(familyIndex, i < 0 ? 0 : i);
        }
    }

    private DeviceQueue? GetQueueByIndexes(int? familyIndex, int queueIndex)
    {
        if (familyIndex is null) return null;
        if (familyIndex > _queueCache.Length)
            throw new ArgumentOutOfRangeException(nameof(familyIndex), familyIndex,
                                                  "Family index is out of bounds, no queue family has that index");

        var queueList = _queueCache[familyIndex.Value];
        if (queueIndex > queueList.Length)
            throw new ArgumentOutOfRangeException(nameof(queueIndex), queueIndex,
                                                  $"Queue index is out of bounds, can't create queue at this index in queue family n°{familyIndex}");

        DeviceQueue queue;
        lock (_lock)
        {
            queue = queueList[queueIndex];

            if (queue is not null) return queue;

            queue = new(this, familyIndex.Value, queueIndex);
            queueList[queueIndex] = queue;
        }

        return queue;
    }

    private Queue GetQueue(int familyIndex, int queueIndex)
    {
        return _api.GetDeviceQueue(_device, (uint)familyIndex, (uint)queueIndex);
    }

    public class DeviceQueue
    {
        internal DeviceQueue(DeviceQueuesManager manager, int familyIndex, int queueIndex)
        {
            _manager = manager;
            FamilyIndex = familyIndex;
            QueueIndex = queueIndex;
            Handle = _manager.GetQueue(familyIndex, queueIndex);
        }

        public int FamilyIndex { get; }
        public int QueueIndex { get; }
        public Queue Handle { get; }

        private readonly DeviceQueuesManager _manager;

        public static implicit operator Queue(DeviceQueue q) => q.Handle;

        public Result WaitIdle()
        {
            return _manager._api.QueueWaitIdle(Handle);
        }
    }
}