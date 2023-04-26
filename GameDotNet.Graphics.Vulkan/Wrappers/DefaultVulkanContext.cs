using System.Diagnostics;
using GameDotNet.Graphics.Vulkan.Bootstrap;
using GameDotNet.Graphics.Vulkan.MemoryAllocation;
using Silk.NET.Core;
using Silk.NET.Core.Contexts;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;

namespace GameDotNet.Graphics.Vulkan.Wrappers;

public sealed class DefaultVulkanContext : IVulkanContext
{
    public Vk Api { get; }
    public VulkanInstance Instance { get; }
    public VulkanPhysDevice PhysDevice { get; }
    public VulkanSurface Surface { get; }
    public VulkanDevice Device { get; }
    public VulkanMemoryAllocator Allocator { get; }
    public VulkanCommandBufferPool Pool { get; }
    public DeviceQueue MainGraphicsQueue { get; }

    public static unsafe (DefaultVulkanContext? context, string info) TryCreateForView(IView view)
    {
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
                           IsValidationLayersRequested = true
#endif
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

        var device = new DeviceBuilder(instance, selected).Build();

        var allocator = new VulkanMemoryAllocator(new(instance.VkVersion, instance.Vk, instance, physDevice, device));

        var prop = selected.Properties;
        var name = SilkMarshal.PtrToString((nint)prop.DeviceName) ?? "N/A";

        var queue = device.QueuesManager.GetFirstGraphic();
        if (queue is null) return (null, "No graphics queue family found");

        var pool = new VulkanCommandBufferPool(instance.Vk, device, queue);

        return (new(instance.Vk, instance, physDevice, surface, device, allocator, pool, queue), name);
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

    private DefaultVulkanContext(Vk api, VulkanInstance instance, VulkanPhysDevice physDevice, VulkanSurface surface,
                                 VulkanDevice device,
                                 VulkanMemoryAllocator allocator, VulkanCommandBufferPool pool,
                                 DeviceQueue mainGraphicsQueue)
    {
        Api = api;
        Instance = instance;
        PhysDevice = physDevice;
        Surface = surface;
        Device = device;
        Allocator = allocator;
        Pool = pool;
        MainGraphicsQueue = mainGraphicsQueue;
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