using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using AutoFactories;
using GameDotNet.Graphics.Vulkan.Abstractions;
using GameDotNet.Graphics.Vulkan.Bootstrap;
using GameDotNet.Graphics.Vulkan.MemoryAllocation;
using GameDotNet.Graphics.Vulkan.Services;
using GameDotNet.Graphics.Vulkan.Tools.Allocators;
using Microsoft.Extensions.Logging;
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
    public DeviceQueuesManager Queues { get; }
    public VulkanMemoryAllocator Allocator { get; }

    private readonly ILogger<DefaultVulkanContext> _logger;

    public DefaultVulkanContext(
        [FromFactory] ILogger<DefaultVulkanContext> logger,
        SelectionCriteria criteria,
        IEnumerable<string> requiredExtensions
    )
    {
        _logger = logger;
        Callbacks = MakeAlloc();
        Api = Vk.GetApi();
        Instance = MakeInstance(requiredExtensions);

        PhysDevice = MakePhysDevice(criteria);
        Device = MakeDevice();
        Queues = new(this);
        Allocator = MakeAllocator();
    }

    public DefaultVulkanContext(
        [FromFactory] ILogger<DefaultVulkanContext> logger,
        ReadOnlySpan<byte> suggestedDeviceLuid
    )
    {
        _logger = logger;
        Callbacks = MakeAlloc();
        Api = Vk.GetApi();
        Instance = MakeInstance(isHeadless: true, requiredExtensions: []);

        var criteria = new SelectionCriteria
        {
            RequiredVersion = Vk.Version13,
            RequirePresent = false,
        };

        if (suggestedDeviceLuid.Length > 0)
        {
            criteria.RequiredDeviceId = suggestedDeviceLuid.ToArray();
        }

        PhysDevice = MakePhysDevice(criteria);
        Device = MakeDevice();
        Queues = new(this);
        Allocator = MakeAllocator();
    }

    public DefaultVulkanContext(
        [FromFactory] ILogger<DefaultVulkanContext> logger,
        IVkSurfaceSource? view = null
    )
    {
        _logger = logger;
        Callbacks = MakeAlloc();
        Api = Vk.GetApi();
        Instance = MakeInstance([], view);

        if (view is not null)
        {
            Surface = CreateSurface(Instance, view);
        }

        var criteria = new SelectionCriteria
        {
            RequiredVersion = Vk.Version13,
            RequirePresent = true,
            DeferSurfaceInit = Surface is null, // if we couldn't create a surface, defer it to the phys device selection step
        };

        PhysDevice = MakePhysDevice(criteria);
        Device = MakeDevice();
        Queues = new(this);
        Allocator = MakeAllocator();
    }

    private IVulkanAllocCallback MakeAlloc()
    {
        return
#if DEBUG
        new TrackedMemoryAllocator(this, "Global");
#else
        new NullAllocator();
#endif
    }

    private VulkanInstance MakeInstance(
        IEnumerable<string> requiredExtensions,
        IVkSurfaceSource? view = null,
        bool isHeadless = false
    )
    {
        var extensions = requiredExtensions.ToList();
        if (view is not null)
        {
            extensions.AddRange(GetRequiredSurfaceExtensions(view));
        }

        var builder = new InstanceBuilder(this)
        {
            ApplicationName = "App",
            EngineName = nameof(GameDotNet),
            EngineVersion = new Version32(0, 0, 1),
            RequiredApiVersion = Vk.Version13,
            IsHeadless = isHeadless,
            AllocCallback = Callbacks.WithUserData("Instance"),
            Extensions = extensions,
#if DEBUG
            EnabledValidationFeatures = new List<ValidationFeatureEnableEXT>
            {
                ValidationFeatureEnableEXT.BestPracticesExt,
                ValidationFeatureEnableEXT.SynchronizationValidationExt,
                //ValidationFeatureEnableEXT.DebugPrintfExt, //do not use with GpuAssistedExt will cause SIGSEV
                ValidationFeatureEnableEXT.GpuAssistedReserveBindingSlotExt,
                ValidationFeatureEnableEXT.GpuAssistedExt,
            },
            IsValidationLayersRequested = true,

            DebugMessageType =
                DebugUtilsMessageTypeFlagsEXT.GeneralBitExt
                | DebugUtilsMessageTypeFlagsEXT.ValidationBitExt
                | DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt,
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

        return builder.Build();
    }

    private SelectedPhysDevice MakePhysDevice(SelectionCriteria criteria)
    {
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
        return new(new(Instance.VkVersion, Api, Instance, PhysDevice.Device, Device));
    }

    public void Dispose()
    {
        Allocator.Dispose();
        Device.Dispose();
        Surface?.Dispose();
        Instance.Dispose();
        Api.Dispose();
    }

    private static unsafe IEnumerable<string> GetRequiredSurfaceExtensions(IVkSurfaceSource view)
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

        var handle = window.VkSurface.Create<nint>(instance.Underlying.ToHandle(), null);
        return new(instance, handle.ToSurface());
    }
}
