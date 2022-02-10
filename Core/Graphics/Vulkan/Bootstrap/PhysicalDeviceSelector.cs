using System.Diagnostics.CodeAnalysis;
using Core.Tools.Extensions;
using dotVariant;
using Silk.NET.Core;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace Core.Graphics.Vulkan.Bootstrap;

public class PhysicalDeviceSelector
{
    private readonly VulkanInstance _instance;
    private readonly Vk _vk;

    public PhysicalDeviceSelector(VulkanInstance instance, SurfaceKHR surface,
                                  SelectionCriteria? selectionCriteria = default)
    {
        Surface = surface;
        Criteria = selectionCriteria ?? new SelectionCriteria();
        _instance = instance;
        _vk = instance.Vk;
    }

    public SurfaceKHR Surface { get; }
    public SelectionCriteria Criteria { get; }

    public VulkanPhysDevice Select()
    {
        if (!_instance.IsHeadless && !Criteria.DeferSurfaceInit && Surface.Handle == 0)
            throw new ArgumentException("No initialized vulkan surface provided.");

        var devices = _instance.GetPhysicalDevices();

        if (devices.Count == 0)
            throw new PlatformException("Couldn't find any physical devices");

        var physDeviceDescriptions = devices.Select(device => PopulateDeviceDetails(device)).ToArray();
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
                    selectedDevice = device;
                }
            }
        }

        if (selectedDevice?.Device.Handle is null)
            throw new PlatformException("No compatible physical device was found");

        return new()
        {
            Device = selectedDevice.Value.Device,
            Surface = Surface,
            InstanceVersion = _instance.VkVersion,
            Features = selectedDevice.Value.DeviceFeatures,
            Properties = selectedDevice.Value.DeviceProperties,
            MemoryProperties = selectedDevice.Value.MemProperties,
            QueueFamilies = selectedDevice.Value.QueueFamilies.ToList(),
            DeferSurfaceInit = Criteria.DeferSurfaceInit,
            ExtensionsToEnable = Criteria.RequiredExtensions
                                         .Concat(CheckDeviceExtSupport(selectedDevice.Value.Device,
                                                                       Criteria.DesiredExtensions))
                                         .ToList()
        };
    }

    private unsafe PhysicalDeviceDesc PopulateDeviceDetails(in PhysicalDevice device)
    {
        uint familyCount = 0;
        _vk.GetPhysicalDeviceQueueFamilyProperties(device, ref familyCount, null);
        var familyProperties = new QueueFamilyProperties[familyCount];
        _vk.GetPhysicalDeviceQueueFamilyProperties(device, familyCount.ToSpan(), familyProperties);

        _vk.GetPhysicalDeviceProperties(device, out var deviceProperties);
        _vk.GetPhysicalDeviceFeatures(device, out var deviceFeatures);
        _vk.GetPhysicalDeviceMemoryProperties(device, out var deviceMemoryProperties);

        var localFeatures = new PhysicalDeviceFeatures2();
        if (_instance.VkVersion >= Vk.Version11 && deviceProperties.ApiVersion >= Vk.Version11)
        {
            _vk.GetPhysicalDeviceFeatures2(device, out localFeatures);
        }
        else if (_instance.SupportsProperties2Ext)
        {
            _vk.TryGetInstanceExtension(_instance.Instance, out KhrGetPhysicalDeviceProperties2 ext);
            ext.GetPhysicalDeviceFeatures2(device, out localFeatures);
        }

        return new()
        {
            Device = device, DeviceFeatures = deviceFeatures, DeviceFeatures2 = localFeatures,
            DeviceProperties = deviceProperties, MemProperties = deviceMemoryProperties,
            QueueFamilies = familyProperties
        };
    }

    private Suitable IsDeviceSuitable(in PhysicalDeviceDesc dsc)
    {
        var suitable = Suitable.Yes;
        if (Criteria.RequiredVersion > dsc.DeviceProperties.ApiVersion)
            return Suitable.No;
        if (Criteria.DesiredVersion > dsc.DeviceProperties.ApiVersion)
            suitable = Suitable.Partial;

        var dedicatedCompute =
            GetDedicatedQueue(dsc.QueueFamilies, QueueFlags.QueueComputeBit, QueueFlags.QueueTransferBit);
        var dedicatedTransfer =
            GetDedicatedQueue(dsc.QueueFamilies, QueueFlags.QueueTransferBit, QueueFlags.QueueComputeBit);

        var separateCompute =
            GetSeparateQueue(dsc.QueueFamilies, QueueFlags.QueueComputeBit, QueueFlags.QueueTransferBit);
        var separateTransfer =
            GetSeparateQueue(dsc.QueueFamilies, QueueFlags.QueueTransferBit, QueueFlags.QueueComputeBit);

        var presentQueue = GetPresentQueue(dsc.Device, Surface, dsc.QueueFamilies);

        if (Criteria.RequireDedicatedComputeQueue && dedicatedCompute is null) return Suitable.No;
        if (Criteria.RequireDedicatedTransferQueue && dedicatedTransfer is null) return Suitable.No;
        if (Criteria.RequireSeparateComputeQueue && separateCompute is null) return Suitable.No;
        if (Criteria.RequireSeparateTransferQueue && separateTransfer is null) return Suitable.No;
        if (Criteria.RequirePresent && presentQueue is null && !Criteria.DeferSurfaceInit) return Suitable.No;

        var requiredExtSupported = CheckDeviceExtSupport(dsc.Device, Criteria.RequiredExtensions).ToArray();
        if (!requiredExtSupported.SequenceEqual(Criteria.RequiredExtensions)) return Suitable.No;

        var desiredExtSupported = CheckDeviceExtSupport(dsc.Device, Criteria.DesiredExtensions).ToArray();
        if (!desiredExtSupported.SequenceEqual(Criteria.DesiredExtensions)) suitable = Suitable.Partial;

        var swapChainAdequate = false;
        if (Criteria.DeferSurfaceInit)
        {
            swapChainAdequate = true;
        }
        else if (!_instance.IsHeadless)
        {
            if (_vk.TryGetInstanceExtension(_instance.Instance, out KhrSurface surfaceExt))
            {
                uint formatCounts = 0;
                surfaceExt.GetPhysicalDeviceSurfaceFormats(dsc.Device, Surface, ref formatCounts, out _);

                uint presentModeCounts = 0;
                surfaceExt.GetPhysicalDeviceSurfacePresentModes(dsc.Device, Surface, ref presentModeCounts, out _);

                swapChainAdequate = formatCounts > 0 && presentModeCounts > 0;
            }
        }

        if (Criteria.RequirePresent && !swapChainAdequate) return Suitable.No;

        if (dsc.DeviceProperties.DeviceType != Criteria.PreferredType)
        {
            if (Criteria.AllowAnyType)
                suitable = Suitable.Partial;
            else return Suitable.No;
        }

        var requiredFeaturesSupported = Criteria.RequiredFeatures == null ||
                                        SupportsFeature(dsc.DeviceFeatures, Criteria.RequiredFeatures.Value);
        if (!requiredFeaturesSupported) return Suitable.No;

        var hasRequiredMemory = false;
        var hasPreferredMemory = false;
        for (var i = 0; i < dsc.MemProperties.MemoryHeapCount; i++)
        {
            if (dsc.MemProperties.MemoryHeaps[i].Size > Criteria.RequiredMemSize)
                hasRequiredMemory = true;

            if (dsc.MemProperties.MemoryHeaps[i].Size > Criteria.DesiredMemSize)
                hasPreferredMemory = true;
        }

        if (!hasRequiredMemory) return Suitable.No;
        return !hasPreferredMemory ? Suitable.Partial : suitable;
    }

    private IEnumerable<string> CheckDeviceExtSupport(PhysicalDevice device, IEnumerable<string> extensions)
    {
        return extensions.Where(extension => _vk.IsDeviceExtensionPresent(device, extension)).ToList();
    }

    private static QueueFamilyProperties? GetDedicatedQueue(IEnumerable<QueueFamilyProperties> families,
                                                            QueueFlags desiredFlags, QueueFlags undesiredFlags)
    {
        foreach (var family in families)
        {
            if (family.QueueFlags.HasFlag(desiredFlags)
                && !family.QueueFlags.HasFlag(undesiredFlags)
                && !family.QueueFlags.HasFlag(QueueFlags.QueueGraphicsBit))
            {
                return family;
            }
        }

        return null;
    }

    private static QueueFamilyProperties? GetSeparateQueue(IEnumerable<QueueFamilyProperties> families,
                                                           QueueFlags desiredFlags, QueueFlags undesiredFlags)
    {
        QueueFamilyProperties? prop = null;
        foreach (var family in families)
        {
            if (!family.QueueFlags.HasFlag(desiredFlags) || family.QueueFlags.HasFlag(QueueFlags.QueueGraphicsBit))
                continue;

            if (!family.QueueFlags.HasFlag(undesiredFlags))
            {
                return family;
            }

            prop = family;
        }

        return prop;
    }

    private QueueFamilyProperties? GetPresentQueue(PhysicalDevice device, SurfaceKHR surface,
                                                   IReadOnlyList<QueueFamilyProperties> families)
    {
        if (surface.Handle == 0)
            return null;

        if (!_vk.TryGetInstanceExtension(_instance.Instance, out KhrSurface ext))
            return null;

        for (var i = 0; i < families.Count; i++)
        {
            var res = ext.GetPhysicalDeviceSurfaceSupport(device, (uint)i, surface, out var presentSupport);
            if (res != Result.Success)
                return null;

            if (presentSupport == true)
                return families[i];
        }

        return null;
    }

    private static bool SupportsFeature(PhysicalDeviceFeatures supported, PhysicalDeviceFeatures requested)
    {
        if (requested.RobustBufferAccess && !supported.RobustBufferAccess) return false;
        if (requested.FullDrawIndexUint32 && !supported.FullDrawIndexUint32) return false;
        if (requested.ImageCubeArray && !supported.ImageCubeArray) return false;
        if (requested.IndependentBlend && !supported.IndependentBlend) return false;
        if (requested.GeometryShader && !supported.GeometryShader) return false;
        if (requested.TessellationShader && !supported.TessellationShader) return false;
        if (requested.SampleRateShading && !supported.SampleRateShading) return false;
        if (requested.DualSrcBlend && !supported.DualSrcBlend) return false;
        if (requested.LogicOp && !supported.LogicOp) return false;
        if (requested.MultiDrawIndirect && !supported.MultiDrawIndirect) return false;
        if (requested.DrawIndirectFirstInstance && !supported.DrawIndirectFirstInstance) return false;
        if (requested.DepthClamp && !supported.DepthClamp) return false;
        if (requested.DepthBiasClamp && !supported.DepthBiasClamp) return false;
        if (requested.FillModeNonSolid && !supported.FillModeNonSolid) return false;
        if (requested.DepthBounds && !supported.DepthBounds) return false;
        if (requested.WideLines && !supported.WideLines) return false;
        if (requested.LargePoints && !supported.LargePoints) return false;
        if (requested.AlphaToOne && !supported.AlphaToOne) return false;
        if (requested.MultiViewport && !supported.MultiViewport) return false;
        if (requested.SamplerAnisotropy && !supported.SamplerAnisotropy) return false;
        if (requested.TextureCompressionEtc2 && !supported.TextureCompressionEtc2) return false;
        if (requested.TextureCompressionAstcLdr && !supported.TextureCompressionAstcLdr) return false;
        if (requested.TextureCompressionBC && !supported.TextureCompressionBC) return false;
        if (requested.OcclusionQueryPrecise && !supported.OcclusionQueryPrecise) return false;
        if (requested.PipelineStatisticsQuery && !supported.PipelineStatisticsQuery) return false;
        if (requested.VertexPipelineStoresAndAtomics && !supported.VertexPipelineStoresAndAtomics) return false;
        if (requested.FragmentStoresAndAtomics && !supported.FragmentStoresAndAtomics) return false;
        if (requested.ShaderTessellationAndGeometryPointSize &&
            !supported.ShaderTessellationAndGeometryPointSize) return false;
        if (requested.ShaderImageGatherExtended && !supported.ShaderImageGatherExtended) return false;
        if (requested.ShaderStorageImageExtendedFormats && !supported.ShaderStorageImageExtendedFormats) return false;
        if (requested.ShaderStorageImageMultisample && !supported.ShaderStorageImageMultisample) return false;
        if (requested.ShaderStorageImageReadWithoutFormat && !supported.ShaderStorageImageReadWithoutFormat)
            return false;
        if (requested.ShaderStorageImageWriteWithoutFormat &&
            !supported.ShaderStorageImageWriteWithoutFormat) return false;
        if (requested.ShaderUniformBufferArrayDynamicIndexing &&
            !supported.ShaderUniformBufferArrayDynamicIndexing) return false;
        if (requested.ShaderSampledImageArrayDynamicIndexing &&
            !supported.ShaderSampledImageArrayDynamicIndexing) return false;
        if (requested.ShaderStorageBufferArrayDynamicIndexing &&
            !supported.ShaderStorageBufferArrayDynamicIndexing) return false;
        if (requested.ShaderStorageImageArrayDynamicIndexing &&
            !supported.ShaderStorageImageArrayDynamicIndexing) return false;
        if (requested.ShaderClipDistance && !supported.ShaderClipDistance) return false;
        if (requested.ShaderCullDistance && !supported.ShaderCullDistance) return false;
        if (requested.ShaderFloat64 && !supported.ShaderFloat64) return false;
        if (requested.ShaderInt64 && !supported.ShaderInt64) return false;
        if (requested.ShaderInt16 && !supported.ShaderInt16) return false;
        if (requested.ShaderResourceResidency && !supported.ShaderResourceResidency) return false;
        if (requested.ShaderResourceMinLod && !supported.ShaderResourceMinLod) return false;
        if (requested.SparseBinding && !supported.SparseBinding) return false;
        if (requested.SparseResidencyBuffer && !supported.SparseResidencyBuffer) return false;
        if (requested.SparseResidencyImage2D && !supported.SparseResidencyImage2D) return false;
        if (requested.SparseResidencyImage3D && !supported.SparseResidencyImage3D) return false;
        if (requested.SparseResidency2Samples && !supported.SparseResidency2Samples) return false;
        if (requested.SparseResidency4Samples && !supported.SparseResidency4Samples) return false;
        if (requested.SparseResidency8Samples && !supported.SparseResidency8Samples) return false;
        if (requested.SparseResidency16Samples && !supported.SparseResidency16Samples) return false;
        if (requested.SparseResidencyAliased && !supported.SparseResidencyAliased) return false;
        if (requested.VariableMultisampleRate && !supported.VariableMultisampleRate) return false;
        if (requested.InheritedQueries && !supported.InheritedQueries) return false;

        //TODO: Add generic features checking and extended features chain

        return true;
    }

    private enum Suitable
    {
        Yes,
        Partial,
        No
    }

    public class SelectionCriteria
    {
        public bool AllowAnyType = true;
        public bool DeferSurfaceInit = false;
        public List<string> DesiredExtensions;
        public ulong DesiredMemSize = 0;
        public Version32 DesiredVersion = Vk.Version10;
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

        public SelectionCriteria()
        {
            RequiredExtensions = new();
            DesiredExtensions = new();
            RequiredFeatures = null;
            RequiredFeatures2 = null;
        }
    }
}

[Variant]
internal readonly partial struct PhysicalDeviceFeatures2Variant
{
    [SuppressMessage("ReSharper", "PartialMethodWithSinglePart")]
    static partial void VariantOf(PhysicalDeviceFeatures2 deviceFeatures2,
                                  PhysicalDeviceFeatures2KHR deviceFeatures2Khr);
}

internal struct PhysicalDeviceDesc
{
    public PhysicalDevice Device { get; set; }
    public IReadOnlyList<QueueFamilyProperties> QueueFamilies { get; set; }

    public PhysicalDeviceFeatures DeviceFeatures { get; set; }
    public PhysicalDeviceProperties DeviceProperties { get; set; }
    public PhysicalDeviceMemoryProperties MemProperties { get; set; }

    //If vulkan version is 1.1 the variant uses PhysicalDeviceFeatures2
    public PhysicalDeviceFeatures2Variant DeviceFeatures2 { get; set; }
}