using System.Collections.Immutable;
using System.Threading.Channels;
using GameDotNet.Core.Tooling;
using GameDotNet.Core.Tooling.Extensions;
using GameDotNet.Graphics.Vulkan.Abstractions;
using GameDotNet.Graphics.Vulkan.Tools;
using GameDotNet.Graphics.Vulkan.Wrappers;
using Microsoft.Extensions.ObjectPool;
using Nito.Disposables;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using ValueTaskSupplement;
using ZLinq;

namespace GameDotNet.Graphics.Vulkan.Services;

public class DeviceQueuesManager : SingleDisposable<EmptyStruct>
{
    private readonly Vk _api;
    private readonly VulkanInstance _instance;
    private readonly VulkanPhysDevice _physDevice;
    private readonly VulkanDevice _device;

    private readonly ImmutableArray<QueueFamilyProperties> _familyProperties;
    private readonly ImmutableArray<ImmutableArray<DeviceQueue>> _queueCache;
    private readonly ImmutableArray<Channel<DeviceQueue>> _availableQueues;
    private readonly ImmutableDictionary<DeviceQueue, QueueHandle> _queueHandles;

    public DeviceQueuesManager(IVulkanContext context)
        : base(default)
    {
        _api = context.Api;
        _instance = context.Instance;
        _physDevice = context.PhysDevice.Device;
        _device = context.Device;

        _familyProperties =
        [
            .. context
                .PhysDevice.Device.GetQueueFamilyProperties2()
                .Select(p => p.QueueFamilyProperties),
        ];

        _queueCache =
        [
            .. Enumerable
                .Range(0, _familyProperties.Length)
                .Select(famIndex =>
                    Enumerable
                        .Range(0, (int)_familyProperties[famIndex].QueueCount)
                        .Select(queueIndex => new DeviceQueue(
                            context,
                            GetQueue(famIndex, queueIndex),
                            (uint)famIndex,
                            (uint)queueIndex
                        ))
                        .ToImmutableArray()
                ),
        ];

        _availableQueues =
        [
            .. _queueCache.Select(queues =>
            {
                //for now we use an unbounded channel but might be interesting to go bounded by the number of queues in the family
                var channel = Channel.CreateUnbounded<DeviceQueue>();

                foreach (var queue in queues)
                {
                    channel.Writer.TryWrite(queue);
                }

                return channel;
            }),
        ];

        _queueHandles = _queueCache
            .SelectMany(queues => queues)
            .Select(queue =>
                (queue, new QueueHandle(queue, _availableQueues[(int)queue.FamilyIndex]))
            )
            .ToImmutableDictionary(tuple => tuple.queue, tuple => tuple.Item2);
    }

    public ValueTask<QueueHandle?> GetFirstGraphic()
    {
        return GetFirstQueue(QueueFlags.GraphicsBit);
    }

    public async ValueTask<QueueHandle?> GetFirstPresent(
        VulkanSurface surface,
        CancellationToken token = default
    )
    {
        var presentFamily = GetPresentQueueFamilyIndex(_instance, _physDevice, surface);

        return presentFamily is not null
            ? await GetAvailableQueue(presentFamily.Value, token)
            : null;
    }

    public async ValueTask<QueueHandle?> GetFirstQueue(
        QueueFlags desiredFlags,
        CancellationToken token = default
    )
    {
        var index = GetFirstQueueFamilyIndex(desiredFlags);

        return index is not null ? await GetAvailableQueue(index.Value, token) : null;
    }

    public async ValueTask<QueueHandle?> GetDedicatedQueue(
        QueueFlags desiredFlags,
        QueueFlags undesiredFlags,
        CancellationToken token = default
    )
    {
        var index = GetDedicatedQueueFamilyIndex(desiredFlags, undesiredFlags);

        return index is not null ? await GetAvailableQueue(index.Value, token) : null;
    }

    public async ValueTask<QueueHandle?> GetSeparateQueue(
        QueueFlags desiredFlags,
        QueueFlags undesiredFlags,
        CancellationToken token = default
    )
    {
        var index = GetSeparateQueueFamilyIndex(desiredFlags, undesiredFlags);

        return index is not null ? await GetAvailableQueue(index.Value, token) : null;
    }

    public async ValueTask<QueueHandle> RequestQueueAsync(
        QueueFlags requiredFlags,
        QueueFlags forbiddenFlags,
        CancellationToken token = default
    )
    {
        using var ctsSrc = CancellationTokenSource.CreateLinkedTokenSource(token);

        var requestTasks = _availableQueues
            .AsValueEnumerable()
            .WithState((_familyProperties, requiredFlags, forbiddenFlags))
            .Where(
                static (t, famIndex) =>
                {
                    var ((properties, reqFlags, forbiddenFlags), _) = t;

                    return properties[famIndex].QueueFlags.HasFlag(reqFlags)
                        && !properties[famIndex].QueueFlags.HasFlag(forbiddenFlags);
                }
            )
            .ReplaceState(ctsSrc.Token)
            .Select(static t =>
            {
                var (cancel, item) = t;

                return item.Reader.ReadAsync(cancel);
            })
            .ToArray();

        var (_, winningQueue) = await ValueTaskEx.WhenAny(requestTasks);

        await ctsSrc.CancelAsync(); //Cancel the wait on other family channels

        return _queueHandles[winningQueue].Reset();
    }

    public sealed class QueueHandle(DeviceQueue queue, ChannelWriter<DeviceQueue> returnChannel)
        : IDisposable,
            IResettable
    {
        public DeviceQueue Queue => queue;
        private bool _disposed = true;

        public void Dispose()
        {
            if (!Interlocked.Exchange(ref _disposed, true))
                returnChannel.TryWrite(queue);
        }

        public bool TryReset()
        {
            return Interlocked.Exchange(ref _disposed, false);
        }

        public QueueHandle Reset()
        {
            TryReset();
            return this;
        }
    }

    internal async ValueTask<QueueHandle> GetAvailableQueue(
        int familyIndex,
        CancellationToken token = default
    )
    {
        if (familyIndex >= _queueCache.Length)
            throw new ArgumentOutOfRangeException(
                nameof(familyIndex),
                familyIndex,
                "Family index is out of bounds, no queue family has that index"
            );

        var queueChannel = _availableQueues[familyIndex];

        var queue = await queueChannel.Reader.ReadAsync(token);

        return _queueHandles[queue].Reset();
    }

    internal int? GetFirstQueueFamilyIndex(QueueFlags desiredFlags) =>
        QueueTools.GetFirstQueueFamilyIndex(_familyProperties, desiredFlags);

    internal int? GetDedicatedQueueFamilyIndex(
        QueueFlags desiredFlags,
        QueueFlags undesiredFlags
    ) => QueueTools.GetDedicatedQueueFamilyIndex(_familyProperties, desiredFlags, undesiredFlags);

    internal int? GetSeparateQueueFamilyIndex(QueueFlags desiredFlags, QueueFlags undesiredFlags) =>
        QueueTools.GetSeparateQueueFamilyIndex(_familyProperties, desiredFlags, undesiredFlags);

    internal int? GetPresentQueueFamilyIndex(
        VulkanInstance instance,
        PhysicalDevice device,
        SurfaceKHR surface
    ) => QueueTools.GetPresentQueueFamilyIndex(instance, device, surface, _familyProperties);

    private Queue GetQueue(int familyIndex, int queueIndex)
    {
        return _api.GetDeviceQueue(_device, (uint)familyIndex, (uint)queueIndex);
    }

    protected override void Dispose(EmptyStruct context)
    {
        foreach (var availableQueue in _availableQueues)
        {
            availableQueue.Writer.Complete();
        }
    }
}
