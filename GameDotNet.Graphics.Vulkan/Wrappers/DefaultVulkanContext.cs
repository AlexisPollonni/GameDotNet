using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using GameDotNet.Graphics.Vulkan.Bootstrap;
using GameDotNet.Graphics.Vulkan.MemoryAllocation;
using GameDotNet.Graphics.Vulkan.Tools.Allocators;
using Silk.NET.Core;
using Silk.NET.Core.Contexts;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;

namespace GameDotNet.Graphics.Vulkan.Wrappers;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public sealed class DefaultVulkanContext : IVulkanContext
{
    public required Vk Api { get; init; }
    public required VulkanInstance Instance { get; init; }
    public required IVulkanAllocCallback Callbacks { get; init; }
    public required VulkanPhysDevice PhysDevice { get; init; }
    public required VulkanSurface Surface { get; init; }
    public required VulkanDevice Device { get; init; }
    public required VulkanMemoryAllocator Allocator { get; init; }
    public required VulkanCommandBufferPool Pool { get; init; }
    public required DeviceQueue MainGraphicsQueue { get; init; }

    public static unsafe (DefaultVulkanContext? context, string info) TryCreateForView(IView view)
    {
        var alloc =
#if DEBUG
            new TrackedMemoryAllocator( "Global");
#else
            new NullAllocator();
#endif

        var instance = new InstanceBuilder
                       {
                           ApplicationName = "App",
                           EngineName = "GamesDotNet",
                           EngineVersion = new Version32(0, 0, 1),
                           RequiredApiVersion = Vk.Version11,
                           Extensions = GetGlfwRequiredVulkanExtensions(view),
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
#endif
                           AllocCallback = alloc.WithUserData("Instance")
                       }
#if DEBUG
                       .UseDefaultDebugMessenger()
#endif
                       .Build();

        var surface = CreateSurface(instance, view);

        var selected = new PhysicalDeviceSelector(instance, surface, new()
        {
            RequiredVersion = Vk.Version11
        }).Select();
        var physDevice = selected.Device;

        var device = new DeviceBuilder(instance, selected)
        {
            AllocationCallbacks = alloc.WithUserData("Device")
        }.Build();

        var allocator = new VulkanMemoryAllocator(new(instance.VkVersion, instance.Vk, instance, physDevice, device));

        var prop = selected.Properties;
        var name = SilkMarshal.PtrToString((nint)prop.DeviceName) ?? "N/A";

        var queue = device.QueuesManager.GetFirstGraphic();
        if (queue is null) return (null, "No graphics queue family found");

        var pool = new VulkanCommandBufferPool(instance.Vk, device, queue, alloc);

        return (new()
                   {
                       Api = instance.Vk,
                       Callbacks = alloc,
                       Instance = instance,
                       PhysDevice = physDevice,
                       Device = device,
                       MainGraphicsQueue = queue,
                       Pool = pool,
                       Surface = surface,
                       Allocator = allocator,
                   }, name);
    }

    public void Dispose()
    {
        Pool.Dispose();
        Allocator.Dispose();
        Device.Dispose();
        Surface.Dispose();
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

    private static unsafe VulkanSurface CreateSurface(VulkanInstance instance, IVkSurfaceSource window)
    {
        Debug.Assert(window.VkSurface != null, "window.VkSurface != null");

        var handle = window.VkSurface.Create<nint>(instance.Instance.ToHandle(), null);
        return new(instance, handle.ToSurface());
    }
}