using GameDotNet.Core.Tooling.Collections;
using GameDotNet.Core.Tooling.Extensions;
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
            for (uint i = 0; i < context.PhysDevice.QueueFamilies.Count; i++)
                queueDesc.Add(new(i, 1, new[] { 1f }));

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

        var hasPhysDevFeatures2 = false;
        var userDefinedPhysDevFeatures2 = _info.NextChain.Any(next =>
            next.AsRef<BaseOutStructure>().SType == StructureType.PhysicalDeviceFeatures2
        );

        var finalNextChain = new List<GlobalMemory>();
        DeviceCreateInfo deviceCreateInfo;

        var physicalDeviceExtensionFeatures = context.PhysDevice.ExtendedFeaturesChain.ToList();
        var localFeatures2 = new PhysicalDeviceFeatures2
        {
            SType = StructureType.PhysicalDeviceFeatures2,
        };

        if (!userDefinedPhysDevFeatures2)
        {
            if (context.PhysDevice.InstanceVersion > Vk.Version11)
            {
                localFeatures2.Features = context.PhysDevice.Features;
                finalNextChain.Add(localFeatures2.ToGlobalMemory().DisposeWith(d));
                hasPhysDevFeatures2 = true;
                finalNextChain.AddRange(
                    physicalDeviceExtensionFeatures.Select(node =>
                        node.ToGlobalMemory().DisposeWith(d)
                    )
                );
            }
        }
        else
        {
            Log.Information(
                "User provided VkPhysicalDeviceFeatures2 instance found in pNext chain, all requirements added via the PhysicalDeviceBuilder will be ignored"
            );
        }

        if (!userDefinedPhysDevFeatures2 && !hasPhysDevFeatures2)
        {
            unsafe
            {
                deviceCreateInfo.PEnabledFeatures = context.PhysDevice.Features.ToPtrPinned(d);
            }
        }

        finalNextChain.AddRange(_info.NextChain);

        var nextArray = finalNextChain.SetupPNextChain().ToArray();

        unsafe
        {
            deviceCreateInfo = new(
                flags: _info.Flags,
                queueCreateInfoCount: (uint)queueCreateInfos.Count,
                enabledExtensionCount: (uint)extensions.Count,
                enabledLayerCount: context.Instance.IsValidationEnabled
                    ? (uint)Constants.DefaultValidationLayers.Length
                    : 0,
                pQueueCreateInfos: queueCreateInfos.ToPtr(d),
                ppEnabledExtensionNames: extensions.ToByteDoublePtr(d),
                ppEnabledLayerNames: context.Instance.IsValidationEnabled
                    ? Constants.DefaultValidationLayers.ToByteDoublePtr(d)
                    : null,
                pNext: nextArray.Length is not 0 ? (void*)nextArray[0].Handle : null
            );
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
        public IList<GlobalMemory> NextChain;
        public IList<CustomQueueDescription> QueueDescriptions;

        public DeviceInfo()
        {
            AllocationCallbacks = new NullAllocator();
            NextChain = new List<GlobalMemory>();
            QueueDescriptions = new List<CustomQueueDescription>();
        }
    }
}
