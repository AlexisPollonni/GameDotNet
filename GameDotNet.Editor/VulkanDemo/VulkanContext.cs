using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Logging;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using GameDotNet.Graphics.Vulkan.Bootstrap;
using GameDotNet.Graphics.Vulkan.MemoryAllocation;
using GameDotNet.Graphics.Vulkan.Tools;
using GameDotNet.Graphics.Vulkan.Wrappers;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Constants = GameDotNet.Core.Constants;
using D3DDevice = SharpDX.Direct3D11.Device;
using Format = Silk.NET.Vulkan.Format;

namespace GameDotNet.Editor.VulkanDemo;

internal sealed class VulkanContext : IDisposable
{
    public Vk Api { get; }

    public VulkanInstance Instance { get; }
    public VulkanPhysDevice PhysDevice { get; }

    public VulkanDevice Device { get; }

    public D3DDevice? D3DDevice { get; }

    public VulkanMemoryAllocator Allocator { get; }

    public VulkanCommandBufferPool Pool { get; }

    private VulkanMemoryPool? _imageMemoryPool;

    public static unsafe (VulkanContext? result, string info) TryCreate(ICompositionGpuInterop gpuInterop)
    {
        var api = Vk.GetApi();

        var verbose = Logger.TryGet(LogEventLevel.Verbose, LogArea.Visual);
        var info = Logger.TryGet(LogEventLevel.Information, LogArea.Visual);
        var warn = Logger.TryGet(LogEventLevel.Warning, LogArea.Visual);
        var err = Logger.TryGet(LogEventLevel.Error, LogArea.Visual);

        var logCallback = new DebugUtilsMessengerCallbackFunctionEXT((severity, types, data, userData) =>
        {
            var msgType = types switch
            {
                DebugUtilsMessageTypeFlagsEXT.GeneralBitExt | DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt |
                    DebugUtilsMessageTypeFlagsEXT.ValidationBitExt =>
                    "General | Validation | Performance",
                DebugUtilsMessageTypeFlagsEXT.ValidationBitExt | DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt =>
                    "Validation | Performance",
                DebugUtilsMessageTypeFlagsEXT.GeneralBitExt | DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt =>
                    "General | Performance",
                DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt => "Performance",
                DebugUtilsMessageTypeFlagsEXT.GeneralBitExt | DebugUtilsMessageTypeFlagsEXT.ValidationBitExt =>
                    "General | Validation",
                DebugUtilsMessageTypeFlagsEXT.ValidationBitExt => "Validation",
                DebugUtilsMessageTypeFlagsEXT.GeneralBitExt => "General",
                _ => "Unknown"
            };

            var msg = SilkMarshal.PtrToString((nint)data->PMessage);

            const string template = "<Vulkan || {MessageType}> {Message}";
            switch (severity)
            {
                case DebugUtilsMessageSeverityFlagsEXT.VerboseBitExt:
                    verbose?.Log(null, template, msgType, msg);
                    break;
                case DebugUtilsMessageSeverityFlagsEXT.InfoBitExt:
                    info?.Log(null, template, msgType, msg);
                    break;
                case DebugUtilsMessageSeverityFlagsEXT.WarningBitExt:
                case DebugUtilsMessageSeverityFlagsEXT.None:
                    warn?.Log(null, template, msgType, msg);
                    break;
                case DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt:
                    err?.Log(null, template, msgType, msg);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(severity), severity, null);
            }

            return Vk.False;
        });

        var instance = new InstanceBuilder(api)
            {
                ApplicationName = "Engine Editor",
                EngineName = Constants.EngineName,
                EngineVersion = new Version32(0, 0, 1),
                RequiredApiVersion = Vk.Version11,
                Extensions = new[]
                {
                    "VK_KHR_get_physical_device_properties2",
                    "VK_KHR_external_memory_capabilities",
                    "VK_KHR_external_semaphore_capabilities"
                },
                IsHeadless = false,
#if DEBUG
                EnabledValidationFeatures = new List<ValidationFeatureEnableEXT>
                {
                    ValidationFeatureEnableEXT.BestPracticesExt,
                    ValidationFeatureEnableEXT.SynchronizationValidationExt,
                    ValidationFeatureEnableEXT.DebugPrintfExt,
                    ValidationFeatureEnableEXT.GpuAssistedReserveBindingSlotExt
                },
                IsValidationLayersRequested = true,
                DebugCallback = logCallback
#endif
            }
            .Build();

        var requiredDeviceExtensions = new List<string>()
        {
            "VK_KHR_external_memory",
            "VK_KHR_external_semaphore"
        };

        if (OperatingSystem.IsWindows())
        {
            if (!gpuInterop.SupportedImageHandleTypes.Contains(KnownPlatformGraphicsExternalImageHandleTypes
                                                                   .D3D11TextureGlobalSharedHandle))
                return (null, "Image sharing is not supported by the current backend");

            requiredDeviceExtensions.Add(KhrExternalMemoryWin32.ExtensionName);
            requiredDeviceExtensions.Add(KhrExternalSemaphoreWin32.ExtensionName);
            requiredDeviceExtensions.Add("VK_KHR_dedicated_allocation");
            requiredDeviceExtensions.Add("VK_KHR_get_memory_requirements2");
        }
        else
        {
            if (!gpuInterop.SupportedImageHandleTypes.Contains(KnownPlatformGraphicsExternalImageHandleTypes
                                                                   .VulkanOpaquePosixFileDescriptor)
                || !gpuInterop.SupportedSemaphoreTypes.Contains(KnownPlatformGraphicsExternalSemaphoreHandleTypes
                                                                    .VulkanOpaquePosixFileDescriptor))
                return (null, "Image sharing is not supported by the current backend");
            requiredDeviceExtensions.Add(KhrExternalMemoryFd.ExtensionName);
            requiredDeviceExtensions.Add(KhrExternalSemaphoreFd.ExtensionName);
        }

        var physDevice = new PhysicalDeviceSelector(instance, null, new()
        {
            RequiredVersion = Vk.Version11,
            RequiredExtensions = requiredDeviceExtensions,
            RequiredDeviceId = gpuInterop.DeviceLuid ?? gpuInterop.DeviceUuid,
            DeferSurfaceInit = true
        }).Select();

        var device = new DeviceBuilder(instance, physDevice).Build();
        var allocator = new VulkanMemoryAllocator(new(instance.VkVersion, api, instance, physDevice, device));

        var queueIndex = device.GetQueueIndex(QueueType.Graphics);
        if (queueIndex is null)
            return (null, "No graphics queue found on the device");

        api.GetDeviceQueue(device, queueIndex.Value, 0, out var queue);

        var cmdBufferPool = new VulkanCommandBufferPool(api, device, queue, queueIndex.Value);

        SharpDX.Direct3D11.Device? d3dDevice = null;
        if (gpuInterop.DeviceLuid is not null && OperatingSystem.IsWindows())
            d3dDevice = D3DMemoryHelper.CreateDeviceByLuid(gpuInterop.DeviceLuid);

        var deviceProperties = physDevice.Properties;
        var deviceName = SilkMarshal.PtrToString((nint)deviceProperties.DeviceName) ?? "Name unknown";
        return (new(api, instance, physDevice, device, d3dDevice, allocator, cmdBufferPool), deviceName);
    }

    public unsafe (VulkanImage image, Texture2D? texture) CreateVulkanImage(
        uint format, PixelSize size, bool exportable)
    {
        var handleType = OperatingSystem.IsWindows()
                             ? ExternalMemoryHandleTypeFlags.D3D11TextureBit
                             : ExternalMemoryHandleTypeFlags.OpaqueFDBit;
        var externalMemoryCreateInfo = new ExternalMemoryImageCreateInfo(handleTypes: handleType);
        var imgInfo = new ImageCreateInfo(pNext: exportable ? &externalMemoryCreateInfo : null,
                                          imageType: ImageType.Type2D,
                                          format: (Format)format,
                                          extent: new((uint)size.Width, (uint)size.Height, 1),
                                          mipLevels: 1,
                                          arrayLayers: 1,
                                          samples: SampleCountFlags.Count1Bit,
                                          tiling: ImageTiling.Optimal,
                                          usage: ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.TransferDstBit |
                                                 ImageUsageFlags.TransferSrcBit | ImageUsageFlags.SampledBit,
                                          sharingMode: SharingMode.Exclusive, initialLayout: ImageLayout.Undefined,
                                          flags: ImageCreateFlags.CreateMutableFormatBit);

        var allocInfo = new AllocationCreateInfo(requiredFlags: MemoryPropertyFlags.DeviceLocalBit,
                                                 usage: MemoryUsage.GPU_Only);

        Texture2D? d3dTex2D = null;
        if (exportable && OperatingSystem.IsWindows())
            d3dTex2D = D3DMemoryHelper.CreateMemoryHandle(D3DDevice!, size, (Format)format);


        if (exportable)
        {
            IChain<MemoryAllocateInfo>? chain;
            if (OperatingSystem.IsWindows())
            {
                using var dxgi = d3dTex2D!.QueryInterface<Resource1>();

                var handleImport =
                    new ImportMemoryWin32HandleInfoKHR(handleType: ExternalMemoryHandleTypeFlags.D3D11TextureBit,
                                                       handle: dxgi.CreateSharedHandle(null,
                                                           SharedResourceFlags.Read |
                                                           SharedResourceFlags.Write));

                chain = Chain.Create(new MemoryAllocateInfo(), handleImport);
            }
            else
            {
                chain = Chain.Create(new MemoryAllocateInfo(),
                                     new ExportMemoryAllocateInfo(handleTypes: handleType));
            }

            allocInfo.MemoryAllocateNext = chain;
        }

        return (new(Api, Device, Allocator, imgInfo, allocInfo), d3dTex2D);
    }

    public void Dispose()
    {
        Allocator.Dispose();
        Pool.Dispose();
        Device.Dispose();
        Instance.Dispose();
        Api.Dispose();

        D3DDevice?.Dispose();
    }

    private VulkanContext(Vk api, VulkanInstance instance, VulkanPhysDevice physDevice, VulkanDevice device,
                          D3DDevice? d3DDevice, VulkanMemoryAllocator allocator, VulkanCommandBufferPool pool)
    {
        Api = api;
        Instance = instance;
        PhysDevice = physDevice;
        Device = device;
        D3DDevice = d3DDevice;
        Allocator = allocator;
        Pool = pool;
    }
}