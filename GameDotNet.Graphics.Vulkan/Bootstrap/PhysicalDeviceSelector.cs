using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance;
using dotVariant;
using GameDotNet.Core.Tooling.Extensions;
using GameDotNet.Graphics.Vulkan.Tools;
using GameDotNet.Graphics.Vulkan.Wrappers;
using Silk.NET.Core;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using ZLinq;

namespace GameDotNet.Graphics.Vulkan.Bootstrap;

public class PhysicalDeviceSelector
{
    private readonly VulkanInstance _instance;
    private readonly Vk _vk;

    public PhysicalDeviceSelector(
        VulkanInstance instance,
        VulkanSurface? surface = null,
        SelectionCriteria? criteria = default
    )
    {
        Surface = surface;
        Criteria = criteria ?? new SelectionCriteria();
        _instance = instance;
        _vk = instance.Context.Api;
    }

    public VulkanSurface? Surface { get; }

    public SelectionCriteria Criteria { get; }

    public SelectedPhysDevice Select()
    {
        if (
            !_instance.IsHeadless
            && !Criteria.DeferSurfaceInit
            && (Surface is null || Surface.AsSurfaceKhr().Handle is 0)
        )
            throw new ArgumentException("No initialized vulkan surface provided.");

        var devices = _instance.GetPhysicalDevices();

        if (devices.Count is 0)
            throw new PlatformException("Couldn't find any physical devices");

        var physDeviceDescriptions = devices
            .Select(device =>
            {
                var clonedChain = ((Chain)Criteria.ExtendedFeaturesChain).Duplicate();

                return PopulateDeviceDetails(device, (IChain<PhysicalDeviceFeatures2>)clonedChain);
            })
            .ToArray();
        PhysicalDeviceDesc? selectedDevice = null;

        if (Criteria.UseFirstGpuUnconditionally)
        {
            selectedDevice = physDeviceDescriptions.FirstOrDefault();
        }
        else
        {
            foreach (var device in physDeviceDescriptions)
            {
                var suitable = IsDeviceSuitable(device);
                if (suitable is Suitable.Yes)
                {
                    selectedDevice = device;
                    break;
                }

                if (suitable is Suitable.Partial)
                {
                    selectedDevice ??= device;
                }
            }
        }

        if (selectedDevice?.Device.Handle is null)
            throw new PlatformException("No compatible physical device was found");

        return new()
        {
            Device = new(_instance.Context, selectedDevice.Value.Device),
            Surface = Surface,
            InstanceVersion = _instance.VkVersion,
            Features = selectedDevice.Value.DeviceFeatures,
            Properties = selectedDevice.Value.DeviceProperties,
            MemoryProperties = selectedDevice.Value.MemProperties,
            QueueFamilies = selectedDevice.Value.QueueFamilies.ToList(),
            DeferSurfaceInit = Criteria.DeferSurfaceInit,
            ExtendedFeaturesChain = Criteria.ExtendedFeaturesChain,
            ExtensionsToEnable = Criteria
                .RequiredExtensions.Concat(
                    CheckDeviceExtSupport(selectedDevice.Value.Device, Criteria.DesiredExtensions)
                )
                .ToList(),
        };
    }

    private unsafe PhysicalDeviceDesc PopulateDeviceDetails(
        in PhysicalDevice device,
        IChain<PhysicalDeviceFeatures2> srcExtendedFeaturesChain
    )
    {
        var physDeviceIdProperties = new PhysicalDeviceIDProperties
        {
            SType = StructureType.PhysicalDeviceIDProperties,
        };
        var physDeviceProperties2 = new PhysicalDeviceProperties2
        {
            SType = StructureType.PhysicalDeviceProperties2,
            PNext = &physDeviceIdProperties,
        };

        uint familyCount = 0;
        _vk.GetPhysicalDeviceQueueFamilyProperties(device, ref familyCount, null);
        var familyProperties = new QueueFamilyProperties[familyCount];
        _vk.GetPhysicalDeviceQueueFamilyProperties(device, familyCount.AsSpan(), familyProperties);

        _vk.GetPhysicalDeviceProperties(device, out var deviceProperties);
        _vk.GetPhysicalDeviceProperties2(device, &physDeviceProperties2);

        _vk.GetPhysicalDeviceFeatures(device, out var deviceFeatures);
        _vk.GetPhysicalDeviceMemoryProperties(device, out var deviceMemoryProperties);

        var defaultFeatures = new PhysicalDeviceFeatures2(features: null);
        ref var localFeatures = ref _instance.VkVersion >= Vk.Version11
        || _instance.SupportsProperties2Ext
            ? ref srcExtendedFeaturesChain.HeadRef
            : ref defaultFeatures;

        if (_instance.VkVersion >= Vk.Version11 && deviceProperties.ApiVersion >= Vk.Version11)
        {
            _vk.GetPhysicalDeviceFeatures2(
                device,
                (PhysicalDeviceFeatures2*)Unsafe.AsPointer(ref localFeatures)
            );
        }
        else if (_instance.SupportsProperties2Ext)
        {
            _vk.TryGetInstanceExtension(_instance, out KhrGetPhysicalDeviceProperties2 ext);
            ext.GetPhysicalDeviceFeatures2(
                device,
                (PhysicalDeviceFeatures2*)Unsafe.AsPointer(ref localFeatures)
            );
        }

        return new()
        {
            Device = device,
            DeviceFeatures = deviceFeatures,
            DeviceFeatures2 = localFeatures,
            DeviceIdProperties = physDeviceIdProperties,
            DeviceProperties = deviceProperties,
            DeviceProperties2 = physDeviceProperties2,
            MemProperties = deviceMemoryProperties,
            QueueFamilies = familyProperties,
            ExtendedFeaturesChain = srcExtendedFeaturesChain,
        };
    }

    private unsafe Suitable IsDeviceSuitable(in PhysicalDeviceDesc dsc)
    {
        var suitable = Suitable.Yes;

        if (Criteria.RequiredDeviceId is not null)
        {
            var idProp = dsc.DeviceIdProperties;
            if (dsc.DeviceIdProperties.DeviceLuidvalid)
            {
                var luid = new ReadOnlySpan<byte>(idProp.DeviceLuid, 8);

                if (!luid.SequenceEqual(Criteria.RequiredDeviceId))
                    return Suitable.No;
            }
            else
            {
                var uuid = new ReadOnlySpan<byte>(idProp.DeviceUuid, 16);

                if (!uuid.SequenceEqual(Criteria.RequiredDeviceId))
                    return Suitable.No;
            }
        }

        if (Criteria.RequiredVersion > dsc.DeviceProperties.ApiVersion)
            return Suitable.No;
        if (Criteria.DesiredVersion > dsc.DeviceProperties.ApiVersion)
            suitable = Suitable.Partial;

        var dedicatedCompute = QueueTools.GetDedicatedQueueFamilyIndex(
            dsc.QueueFamilies,
            QueueFlags.ComputeBit,
            QueueFlags.TransferBit
        );
        var dedicatedTransfer = QueueTools.GetDedicatedQueueFamilyIndex(
            dsc.QueueFamilies,
            QueueFlags.TransferBit,
            QueueFlags.ComputeBit
        );

        var separateCompute = QueueTools.GetSeparateQueueFamilyIndex(
            dsc.QueueFamilies,
            QueueFlags.ComputeBit,
            QueueFlags.TransferBit
        );
        var separateTransfer = QueueTools.GetSeparateQueueFamilyIndex(
            dsc.QueueFamilies,
            QueueFlags.TransferBit,
            QueueFlags.ComputeBit
        );

        var presentQueue = Surface is null
            ? null
            : QueueTools.GetPresentQueueFamilyIndex(
                _instance,
                dsc.Device,
                Surface,
                dsc.QueueFamilies
            );

        if (Criteria.RequireDedicatedComputeQueue && dedicatedCompute is null)
            return Suitable.No;
        if (Criteria.RequireDedicatedTransferQueue && dedicatedTransfer is null)
            return Suitable.No;
        if (Criteria.RequireSeparateComputeQueue && separateCompute is null)
            return Suitable.No;
        if (Criteria.RequireSeparateTransferQueue && separateTransfer is null)
            return Suitable.No;
        if (Criteria.RequirePresent && presentQueue is null && !Criteria.DeferSurfaceInit)
            return Suitable.No;

        var requiredExtSupported = CheckDeviceExtSupport(dsc.Device, Criteria.RequiredExtensions)
            .ToArray();
        if (!requiredExtSupported.SequenceEqual(Criteria.RequiredExtensions))
            return Suitable.No;

        var desiredExtSupported = CheckDeviceExtSupport(dsc.Device, Criteria.DesiredExtensions)
            .ToArray();
        if (!desiredExtSupported.SequenceEqual(Criteria.DesiredExtensions))
            suitable = Suitable.Partial;

        var swapChainAdequate = false;
        if (Criteria.DeferSurfaceInit)
        {
            swapChainAdequate = true;
        }
        else if (!_instance.IsHeadless)
        {
            if (_vk.TryGetInstanceExtension(_instance, out KhrSurface surfaceExt))
            {
                uint formatCounts = 0;
                surfaceExt.GetPhysicalDeviceSurfaceFormats(
                    dsc.Device,
                    Surface!,
                    ref formatCounts,
                    null
                );

                uint presentModeCounts = 0;
                surfaceExt.GetPhysicalDeviceSurfacePresentModes(
                    dsc.Device,
                    Surface!,
                    ref presentModeCounts,
                    null
                );

                swapChainAdequate = formatCounts > 0 && presentModeCounts > 0;
            }
        }

        if (Criteria.RequirePresent && !swapChainAdequate)
            return Suitable.No;

        if (dsc.DeviceProperties.DeviceType != Criteria.PreferredType)
        {
            if (Criteria.AllowAnyType)
                suitable = Suitable.Partial;
            else
                return Suitable.No;
        }

        var requiredFeaturesSupported =
            Criteria.RequiredFeatures is null
            || SupportsFeature(
                dsc.DeviceFeatures,
                Criteria.RequiredFeatures.Value,
                dsc.ExtendedFeaturesChain,
                Criteria.ExtendedFeaturesChain
            );
        if (!requiredFeaturesSupported)
            return Suitable.No;

        var hasRequiredMemory = false;
        var hasPreferredMemory = false;
        for (var i = 0; i < dsc.MemProperties.MemoryHeapCount; i++)
        {
            if (dsc.MemProperties.MemoryHeaps[i].Size > Criteria.RequiredMemSize)
                hasRequiredMemory = true;

            if (dsc.MemProperties.MemoryHeaps[i].Size > Criteria.DesiredMemSize)
                hasPreferredMemory = true;
        }

        if (!hasRequiredMemory)
            return Suitable.No;
        return !hasPreferredMemory ? Suitable.Partial : suitable;
    }

    private IEnumerable<string> CheckDeviceExtSupport(
        PhysicalDevice device,
        IEnumerable<string> extensions
    )
    {
        return extensions
            .Where(extension => _vk.IsDeviceExtensionPresent(device, extension))
            .ToList();
    }

    private static unsafe bool SupportsFeature(
        PhysicalDeviceFeatures supported,
        PhysicalDeviceFeatures requested,
        IChain<PhysicalDeviceFeatures2> extensionSupported,
        IChain<PhysicalDeviceFeatures2> extensionRequested
    )
    {
        var reqFeats = requested.AsSpan().Cast<PhysicalDeviceFeatures, Bool32>();
        var supportedFeats = supported.AsSpan().Cast<PhysicalDeviceFeatures, Bool32>();

        var supportsFeatures = SupportsFeatures(reqFeats, supportedFeats);

        var supportsFeaturesExt = extensionRequested
            .Zip(extensionSupported)
            .Skip(1)
            .All(static tuple =>
            {
                var (req, sup) = tuple;

                using var reqSpan = GetFieldSpanFromChainable(req, out var reqFeatSpan);
                using var supSpan = GetFieldSpanFromChainable(sup, out var supFeatSpan);

                var supports = SupportsFeatures(reqFeatSpan, supFeatSpan);

                return supports;
            });

        return supportsFeatures && supportsFeaturesExt;

        static IDisposable GetFieldSpanFromChainable(IChainable chainItem, out Span<Bool32> span)
        {
            var gcHandle = new PinnedGCHandle<IChainable>(chainItem);
            var sizeofStruct = Marshal.SizeOf(chainItem);

            span = new Span<byte>(gcHandle.GetAddressOfObjectData(), sizeofStruct)
                .Slice(Marshal.SizeOf<BaseOutStructure>()) //Skip the sType and pNext fields
                .Cast<byte, Bool32>();

            return gcHandle;
        }

        static bool SupportsFeatures(
            ReadOnlySpan<Bool32> requiredSpan,
            ReadOnlySpan<Bool32> supportedSpan
        )
        {
            return requiredSpan
                .AsValueEnumerable()
                .Zip(supportedSpan.AsValueEnumerable(), (req, sup) => (req, sup))
                .All(tuple => tuple.req ? tuple.sup : true);
        }
    }

    private enum Suitable
    {
        Yes,
        Partial,
        No,
    }
}

public class SelectionCriteria
{
    public bool AllowAnyType = true;
    public bool DeferSurfaceInit = false;
    public List<string> DesiredExtensions;
    public ulong DesiredMemSize = 0;
    public Version32 DesiredVersion = Vk.Version10;

    public IChain<PhysicalDeviceFeatures2> ExtendedFeaturesChain;

    public PhysicalDeviceType PreferredType = PhysicalDeviceType.DiscreteGpu;
    public bool RequireDedicatedComputeQueue = false;
    public bool RequireDedicatedTransferQueue = false;

    public List<string> RequiredExtensions;

    public PhysicalDeviceFeatures? RequiredFeatures;
    public PhysicalDeviceFeatures2? RequiredFeatures2;

    public ulong RequiredMemSize = 0;
    public Version32 RequiredVersion = Vk.Version10;
    public bool RequirePresent = true;
    public bool RequireSeparateComputeQueue = false;
    public bool RequireSeparateTransferQueue = false;
    public bool UseFirstGpuUnconditionally = false;

    public byte[]? RequiredDeviceId { get; set; }

    public unsafe SelectionCriteria()
    {
        RequiredExtensions = new();
        DesiredExtensions = new();
        ExtendedFeaturesChain = Chain.Create<PhysicalDeviceFeatures2>(
            new(sType: StructureType.PhysicalDeviceFeatures2)
        );
        RequiredFeatures = null;
        RequiredFeatures2 = null;
    }
}

[Variant]
internal readonly partial struct PhysicalDeviceFeatures2Variant
{
    [SuppressMessage("ReSharper", "PartialMethodWithSinglePart")]
    static partial void VariantOf(
        PhysicalDeviceFeatures2 deviceFeatures2,
        PhysicalDeviceFeatures2KHR deviceFeatures2Khr
    );
}

internal struct PhysicalDeviceDesc
{
    public PhysicalDevice Device { get; set; }
    public IReadOnlyList<QueueFamilyProperties> QueueFamilies { get; set; }

    public PhysicalDeviceFeatures DeviceFeatures { get; set; }
    public PhysicalDeviceIDProperties DeviceIdProperties { get; set; }
    public PhysicalDeviceProperties DeviceProperties { get; set; }
    public PhysicalDeviceProperties2 DeviceProperties2 { get; set; }
    public PhysicalDeviceMemoryProperties MemProperties { get; set; }

    //If vulkan version is 1.1 the variant uses PhysicalDeviceFeatures2
    public PhysicalDeviceFeatures2Variant DeviceFeatures2 { get; set; }
    public IChain<PhysicalDeviceFeatures2> ExtendedFeaturesChain { get; set; }
}
