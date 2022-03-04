using GameDotNet.Core.Tools;
using GameDotNet.Core.Tools.Extensions;
using Serilog;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace GameDotNet.Core.Graphics.Vulkan.Bootstrap;

public class DeviceBuilder
{
    private readonly DeviceInfo _info;
    private readonly VulkanInstance _instance;
    private readonly VulkanPhysDevice _physDevice;
    private readonly Vk _vk;


    public DeviceBuilder(VulkanInstance instance, VulkanPhysDevice physDevice)
    {
        _vk = instance.Vk;
        _instance = instance;
        _physDevice = physDevice;
        _info = new();
    }

    public AllocationCallbacks? AllocationCallbacks
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
        using var disposables = new CompositeDisposable();

        var queueDesc = _info.QueueDescriptions.ToList();
        if (queueDesc.Count == 0)
            for (uint i = 0; i < _physDevice.QueueFamilies.Count; i++)
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
                    PQueuePriorities = desc.Priorities.ToGlobalMemory().DisposeWith(disposables).AsPtr<float>()
                };
                queueCreateInfos.Add(info);
            }
        }

        var extensions = _physDevice.ExtensionsToEnable.ToList();
        if (_physDevice.Surface.Handle != 0 || _physDevice.DeferSurfaceInit)
        {
            extensions.Add(KhrSwapchain.ExtensionName);
        }

        var hasPhysDevFeatures2 = false;
        var userDefinedPhysDevFeatures2 =
            _info.NextChain.Any(next => next.AsRef<BaseOutStructure>().SType == StructureType.PhysicalDeviceFeatures2);

        var finalNextChain = new List<GlobalMemory>();
        var deviceCreateInfo = new DeviceCreateInfo();

        var physicalDeviceExtensionFeatures = _physDevice.ExtendedFeaturesChain.ToList();
        var localFeatures2 = new PhysicalDeviceFeatures2();

        if (!userDefinedPhysDevFeatures2)
        {
            if (_physDevice.InstanceVersion > Vk.Version11)
            {
                localFeatures2.Features = _physDevice.Features;
                finalNextChain.Add(localFeatures2.ToGlobalMemory().DisposeWith(disposables));
                hasPhysDevFeatures2 = true;
                finalNextChain.AddRange(physicalDeviceExtensionFeatures.Select(node => node.ToGlobalMemory()
                                                                                   .DisposeWith(disposables)));
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
                deviceCreateInfo.PEnabledFeatures = _physDevice.Features
                                                               .ToGlobalMemory()
                                                               .DisposeWith(disposables)
                                                               .AsPtr<PhysicalDeviceFeatures>();
            }
        }

        finalNextChain.AddRange(_info.NextChain);

        var nextArray = finalNextChain.ToArray();
        SilkExtensions.SetupPNextChain(nextArray);

        deviceCreateInfo.SType = StructureType.DeviceCreateInfo;
        deviceCreateInfo.Flags = _info.Flags;
        deviceCreateInfo.QueueCreateInfoCount = (uint)queueCreateInfos.Count;
        deviceCreateInfo.EnabledExtensionCount = (uint)extensions.Count;
        unsafe
        {
            deviceCreateInfo.PQueueCreateInfos =
                queueCreateInfos.ToGlobalMemory().DisposeWith(disposables).AsPtr<DeviceQueueCreateInfo>();
            deviceCreateInfo.PpEnabledExtensionNames =
                extensions.ToGlobalMemory().DisposeWith(disposables).AsByteDoublePtr();
        }

        Device device;
        Result res;
        if (AllocationCallbacks is null)
        {
            unsafe
            {
                res = _vk.CreateDevice(_physDevice.Device, deviceCreateInfo, null, out device);
            }
        }
        else res = _vk.CreateDevice(_physDevice.Device, deviceCreateInfo, AllocationCallbacks.Value, out device);

        if (res != Result.Success)
            throw new VulkanException(res);

        return new(_instance, _physDevice, device, _physDevice.Surface, _physDevice.QueueFamilies, AllocationCallbacks);
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
        public AllocationCallbacks? AllocationCallbacks;
        public uint Flags;
        public IList<GlobalMemory> NextChain;
        public IList<CustomQueueDescription> QueueDescriptions;

        public DeviceInfo()
        {
            NextChain = new List<GlobalMemory>();
            QueueDescriptions = new List<CustomQueueDescription>();
        }
    }
}