using System.Runtime.CompilerServices;
using GameDotNet.Core.Tooling.Collections;
using GameDotNet.Core.Tooling.Extensions;
using GameDotNet.Graphics.Vulkan.Abstractions;
using GameDotNet.Graphics.Vulkan.Tools;
using GameDotNet.Graphics.Vulkan.Tools.Allocators;
using GameDotNet.Graphics.Vulkan.Tools.Extensions;
using GameDotNet.Graphics.Vulkan.Wrappers;
using Serilog;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace GameDotNet.Graphics.Vulkan.Bootstrap;

public class DeviceBuilder(IVulkanContext context)
{
    private readonly DeviceInfo _info = new();

    public IVulkanAllocCallback AllocationCallbacks
    {
        get => _info.AllocationCallbacks;
        set => _info.AllocationCallbacks = value;
    }

    public VulkanDevice Build()
    {
        using var d = new DisposableList();

        var queueDesc = _info.QueueDescriptions.ToList();
        if (queueDesc.Count == 0)
        {
            //by default request all queues from all families with priority 1
            var allFamilies = context.PhysDevice.QueueFamilies.Select(
                (family, i) =>
                    new CustomQueueDescription(
                        (uint)i,
                        family.QueueCount,
                        Enumerable.Repeat(1f, (int)family.QueueCount).ToList()
                    )
            );

            queueDesc.AddRange(allFamilies);
        }

        var queueCreateInfos = new List<DeviceQueueCreateInfo>();
        foreach (var desc in queueDesc)
        {
            unsafe
            {
                var info = new DeviceQueueCreateInfo
                {
                    SType = StructureType.DeviceQueueCreateInfo,
                    QueueFamilyIndex = desc.Index,
                    QueueCount = desc.Count,
                    PQueuePriorities = desc
                        .Priorities.ToGlobalMemory()
                        .DisposeWith(d)
                        .AsPtr<float>(),
                };
                queueCreateInfos.Add(info);
            }
        }

        var extensions = context.PhysDevice.ExtensionsToEnable.ToList();
        if (context.PhysDevice.Surface is not null || context.PhysDevice.DeferSurfaceInit)
        {
            extensions.Add(KhrSwapchain.ExtensionName);
        }

        var finalNextChain = _info.NextChain;
        var physicalDeviceExtensionFeatures = context.PhysDevice.ExtendedFeaturesChain;

        physicalDeviceExtensionFeatures.HeadRef.Features = context.PhysDevice.Features;

        ref var deviceCreateInfo = ref finalNextChain.HeadRef;

        unsafe
        {
            deviceCreateInfo.Flags = _info.Flags;
            deviceCreateInfo.QueueCreateInfoCount = (uint)queueCreateInfos.Count;
            deviceCreateInfo.EnabledExtensionCount = (uint)extensions.Count;
            deviceCreateInfo.EnabledLayerCount = context.Instance.IsValidationEnabled
                ? (uint)Constants.DefaultValidationLayers.Length
                : 0;
            deviceCreateInfo.PQueueCreateInfos = queueCreateInfos.ToPtr(d);
            deviceCreateInfo.PpEnabledExtensionNames = extensions.ToByteDoublePtr(d);
            deviceCreateInfo.PpEnabledLayerNames = context.Instance.IsValidationEnabled
                ? Constants.DefaultValidationLayers.ToByteDoublePtr(d)
                : null;
            deviceCreateInfo.PNext = Unsafe.AsPointer(ref physicalDeviceExtensionFeatures.HeadRef); //TODO: in the future a cleaner way would be to create a mutable Chain derivative that can be created from an existing chain
        }

        ref readonly var callback = ref _info.AllocationCallbacks.Handle;
        var res = context.Api.CreateDevice(
            context.PhysDevice.Device,
            in deviceCreateInfo,
            in callback,
            out var device
        );

        return res != Result.Success ? throw new VulkanException(res) : new(context, device);
    }

    public readonly struct CustomQueueDescription(uint index, uint count, IList<float> priorities)
    {
        public uint Index { get; } = index;
        public uint Count { get; } = count;
        public IList<float> Priorities { get; } = priorities;
    }

    private class DeviceInfo
    {
        public IVulkanAllocCallback AllocationCallbacks;
        public uint Flags;
        public IChain<DeviceCreateInfo> NextChain;
        public IList<CustomQueueDescription> QueueDescriptions;

        public DeviceInfo()
        {
            unsafe
            {
                NextChain = Chain.Create<DeviceCreateInfo>(new(pNext: null));
            }
            AllocationCallbacks = new NullAllocator();
            QueueDescriptions = new List<CustomQueueDescription>();
        }
    }
}
