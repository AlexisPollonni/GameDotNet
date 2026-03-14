using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using AutoFactories;
using GameDotNet.Graphics.Vulkan.Bootstrap;
using GameDotNet.Graphics.Vulkan.MemoryAllocation;
using GameDotNet.Graphics.Vulkan.Tools.Allocators;
using Microsoft.Extensions.Logging;
using Shouldly;
using Silk.NET.Core;
using Silk.NET.Core.Contexts;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;

namespace GameDotNet.Graphics.Vulkan.Wrappers;

public partial class VulkanContextFactory { }

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[AutoFactory(typeof(VulkanContextFactory), "CreateFrom", ExposeAs = typeof(IVulkanContext))]
public sealed class DefaultVulkanContext : IVulkanContext
{
    public Vk Api { get; }
    public VulkanInstance Instance { get; }
    public IVulkanAllocCallback Callbacks { get; }
    public SelectedPhysDevice PhysDevice { get; }
    public VulkanSurface? Surface { get; }
    public VulkanDevice Device { get; }
    public VulkanMemoryAllocator Allocator { get; }
    public VulkanCommandBufferPool Pool { get; }
    public DeviceQueue MainGraphicsQueue { get; }

    private readonly ILogger<DefaultVulkanContext> _logger;

    public DefaultVulkanContext(
        [FromFactory] ILogger<DefaultVulkanContext> logger,
        ReadOnlySpan<byte> suggestedDeviceLuid
    )
    {
        _logger = logger;
        Callbacks = MakeAlloc();
        Instance = MakeInstance();
        Api = Instance.Vk;

        PhysDevice = MakePhysDevice(suggestedDeviceLuid);
        Device = MakeDevice();
        Allocator = MakeAllocator();
        MainGraphicsQueue = MakeQueue();
        Pool = MakePool();
    }

    public DefaultVulkanContext(
        [FromFactory] ILogger<DefaultVulkanContext> logger,
        IVkSurfaceSource? view = null
    )
    {
        _logger = logger;
        Callbacks = MakeAlloc();
        Instance = MakeInstance(view);
        Api = Instance.Vk;

        if (view is not null)
        {
            Surface = CreateSurface(Instance, view);
        }

        PhysDevice = MakePhysDevice([]);
        Device = MakeDevice();
        Allocator = MakeAllocator();
        MainGraphicsQueue = MakeQueue();
        Pool = MakePool();
    }

    private static IVulkanAllocCallback MakeAlloc()
    {
        return
#if DEBUG
        new TrackedMemoryAllocator("Global");
#else
        new NullAllocator();
#endif
    }

    private VulkanInstance MakeInstance(IVkSurfaceSource? view = null)
    {
        var builder = new InstanceBuilder
        {
            ApplicationName = "App",
            EngineName = nameof(GameDotNet),
            EngineVersion = new Version32(0, 0, 1),
            RequiredApiVersion = Vk.Version13,
            IsHeadless = view is null,
            AllocCallback = Callbacks.WithUserData("Instance"),
#if DEBUG
            EnabledValidationFeatures = new List<ValidationFeatureEnableEXT>
            {
                ValidationFeatureEnableEXT.BestPracticesExt,
                ValidationFeatureEnableEXT.SynchronizationValidationExt,
                ValidationFeatureEnableEXT.DebugPrintfExt,
                ValidationFeatureEnableEXT.GpuAssistedReserveBindingSlotExt,
                ValidationFeatureEnableEXT.GpuAssistedExt,
            },
            IsValidationLayersRequested = true,

            DebugMessageType =
                DebugUtilsMessageTypeFlagsEXT.GeneralBitExt
                | DebugUtilsMessageTypeFlagsEXT.ValidationBitExt
                | DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt
                | DebugUtilsMessageTypeFlagsEXT.DeviceAddressBindingBitExt,
            DebugMessageSeverity =
                DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt
                | DebugUtilsMessageSeverityFlagsEXT.WarningBitExt
                | DebugUtilsMessageSeverityFlagsEXT.InfoBitExt
                | DebugUtilsMessageSeverityFlagsEXT.VerboseBitExt,
#endif
        };
#if DEBUG
        builder.UseDefaultDebugMessenger(_logger);
#endif

        if (view is not null)
        {
            builder.Extensions = GetGlfwRequiredVulkanExtensions(view);
        }

        return builder.Build();
    }

    private SelectedPhysDevice MakePhysDevice(ReadOnlySpan<byte> suggestedDeviceLuid)
    {
        var criteria = new PhysicalDeviceSelector.SelectionCriteria
        {
            RequiredVersion = Vk.Version13,
            RequirePresent = false,
        };

        if (suggestedDeviceLuid.Length > 0)
        {
            criteria.RequiredDeviceId = suggestedDeviceLuid.ToArray();
        }

        return new PhysicalDeviceSelector(Instance, Surface, criteria).Select();
    }

    private VulkanDevice MakeDevice()
    {
        return new DeviceBuilder(this)
        {
            AllocationCallbacks = Callbacks.WithUserData("Device"),
        }.Build();
    }

    private VulkanMemoryAllocator MakeAllocator()
    {
        return new(new(Instance.VkVersion, Instance.Vk, Instance, PhysDevice.Device, Device));
    }

    private DeviceQueue MakeQueue()
    {
        return Device
            .QueuesManager.GetFirstGraphic()
            .ShouldNotBeNull("No graphics queue family found");
    }

    private VulkanCommandBufferPool MakePool()
    {
        return new(this, MainGraphicsQueue);
    }

    public void Dispose()
    {
        Pool.Dispose();
        Allocator.Dispose();
        Device.Dispose();
        Surface?.Dispose();
        Instance.Dispose();
        Api.Dispose();
    }

    private static unsafe IEnumerable<string> GetGlfwRequiredVulkanExtensions(IVkSurfaceSource view)
    {
        Debug.Assert(view.VkSurface != null, "_window.VkSurface != null");
        var ppExtensions = view.VkSurface.GetRequiredExtensions(out var count);

        if (ppExtensions is null)
            throw new PlatformException("Vulkan extensions for windowing not available");
        return SilkMarshal.PtrToStringArray((nint)ppExtensions, (int)count);
    }

    private static unsafe VulkanSurface CreateSurface(
        VulkanInstance instance,
        IVkSurfaceSource window
    )
    {
        Debug.Assert(window.VkSurface != null, "window.VkSurface != null");

        var handle = window.VkSurface.Create<nint>(instance.Instance.ToHandle(), null);
        return new(instance, handle.ToSurface());
    }
}
