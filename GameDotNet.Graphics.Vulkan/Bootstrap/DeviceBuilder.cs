using GameDotNet.Core.Tools.Containers;
using GameDotNet.Core.Tools.Extensions;
using GameDotNet.Graphics.Vulkan.Tools;
using GameDotNet.Graphics.Vulkan.Tools.Allocators;
using GameDotNet.Graphics.Vulkan.Tools.Extensions;
using GameDotNet.Graphics.Vulkan.Wrappers;
using Serilog;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace GameDotNet.Graphics.Vulkan.Bootstrap;

public class DeviceBuilder
{
    private readonly DeviceInfo _info;
    private readonly VulkanInstance _instance;
    private readonly SelectedPhysDevice _selectedPhysDevice;
    private readonly Vk _vk;


    public DeviceBuilder(VulkanInstance instance, SelectedPhysDevice selectedPhysDevice)
    {
        _vk = instance.Vk;
        _instance = instance;
        _selectedPhysDevice = selectedPhysDevice;
        _info = new();
    }

    public IVulkanAllocCallback AllocationCallbacks
    {
        get => _info.AllocationCallbacks;
        set => _info.AllocationCallbacks = value;
    }

    public DeviceBuilder AddNext<T>(T node) where T : unmanaged, IExtendsChain<DeviceCreateInfo>
    {
        _info.NextChain.Add(node.ToGlobalMemory());
        return this;
    }

    public VulkanDevice Build()
    {
        using var d = new DisposableList();

        var queueDesc = _info.QueueDescriptions.ToList();
        if (queueDesc.Count == 0)
            for (uint i = 0; i < _selectedPhysDevice.QueueFamilies.Count; i++)
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
                    PQueuePriorities = desc.Priorities.ToGlobalMemory().DisposeWith(d).AsPtr<float>()
                };
                queueCreateInfos.Add(info);
            }
        }

        var extensions = _selectedPhysDevice.ExtensionsToEnable.ToList();
        if (_selectedPhysDevice.Surface is not null || _selectedPhysDevice.DeferSurfaceInit)
        {
            extensions.Add(KhrSwapchain.ExtensionName);
        }

        var hasPhysDevFeatures2 = false;
        var userDefinedPhysDevFeatures2 =
            _info.NextChain.Any(next => next.AsRef<BaseOutStructure>().SType == StructureType.PhysicalDeviceFeatures2);

        var finalNextChain = new List<GlobalMemory>();
        DeviceCreateInfo deviceCreateInfo;

        var physicalDeviceExtensionFeatures = _selectedPhysDevice.ExtendedFeaturesChain.ToList();
        var localFeatures2 = new PhysicalDeviceFeatures2();

        if (!userDefinedPhysDevFeatures2)
        {
            if (_selectedPhysDevice.InstanceVersion > Vk.Version11)
            {
                localFeatures2.Features = _selectedPhysDevice.Features;
                finalNextChain.Add(localFeatures2.ToGlobalMemory().DisposeWith(d));
                hasPhysDevFeatures2 = true;
                finalNextChain.AddRange(physicalDeviceExtensionFeatures.Select(node => node.ToGlobalMemory()
                                                                                   .DisposeWith(d)));
            }
        }
        else
        {
            Log.Information("User provided VkPhysicalDeviceFeatures2 instance found in pNext chain, all requirements added via the PhysicalDeviceBuilder will be ignored");
        }

        if (!userDefinedPhysDevFeatures2 && !hasPhysDevFeatures2)
        {
            unsafe
            {
                deviceCreateInfo.PEnabledFeatures = _selectedPhysDevice.Features.ToPtrPinned(d);
            }
        }

        finalNextChain.AddRange(_info.NextChain);

        var nextArray = finalNextChain.SetupPNextChain().ToArray();

        unsafe
        {
            deviceCreateInfo =
                new(flags: _info.Flags,
                    queueCreateInfoCount: (uint)queueCreateInfos.Count,
                    enabledExtensionCount: (uint)extensions.Count,
                    enabledLayerCount: _instance.IsValidationEnabled
                                           ? (uint)Constants.DefaultValidationLayers.Length
                                           : 0,
                    pQueueCreateInfos: queueCreateInfos.ToPtr(d),
                    ppEnabledExtensionNames: extensions.ToByteDoublePtr(d),
                    ppEnabledLayerNames: _instance.IsValidationEnabled
                                             ? Constants.DefaultValidationLayers.AsPtr()
                                             : null,
                    pNext: nextArray.Length is not 0 ? (void*)nextArray[0].Handle : null);
        }

        ref readonly var callback = ref _info.AllocationCallbacks.Handle;
        var res = _vk.CreateDevice(_selectedPhysDevice.Device, deviceCreateInfo, callback, out var device);

        if (res != Result.Success)
            throw new VulkanException(res);

        return new(_instance, _selectedPhysDevice.Device, device, _info.AllocationCallbacks);
    }

    public struct CustomQueueDescription
    {
        public CustomQueueDescription(uint index, uint count, IList<float> priorities)
        {
            Index = index;
            Count = count;
            Priorities = priorities;
        }

        public uint Index { get; }
        public uint Count { get; }
        public IList<float> Priorities { get; }
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