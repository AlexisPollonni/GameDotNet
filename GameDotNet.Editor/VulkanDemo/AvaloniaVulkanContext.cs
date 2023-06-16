using Avalonia.Vulkan;
using GameDotNet.Graphics.Vulkan.MemoryAllocation;
using GameDotNet.Graphics.Vulkan.Tools;
using GameDotNet.Graphics.Vulkan.Wrappers;
using Silk.NET.Core.Contexts;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace GameDotNet.Editor.VulkanDemo;

internal sealed class AvaloniaVulkanContext : IVulkanContext
{
    public required Vk Api { get; init; }
    public required VulkanInstance Instance { get; init; }
    public required IVulkanAllocCallback Callbacks { get; init; }
    public required VulkanPhysDevice PhysDevice { get; init; }
    public required VulkanDevice Device { get; init; }
    public required VulkanMemoryAllocator Allocator { get; init; }
    public required VulkanCommandBufferPool Pool { get; init; }

    public static unsafe (AvaloniaVulkanContext? context, string info) TryCreate(IVulkanSharedDevice shared)
    {
        var alloc =
#if DEBUG
            new DebugLoggingVulkanAllocator(Log.Logger, "Global");
#else
            new NullAllocator();
#endif

        var api = GetApi(shared.Device);
        var version = Vk.Version11;

        var vkInstance = new Instance(shared.Device.Instance.Handle);
        var supportsProperty2 = api.IsDeviceExtensionPresent(vkInstance, KhrGetPhysicalDeviceProperties2.ExtensionName);
        const bool isValidationEnabled =
#if DEBUG
            true;
#else
            false;
#endif

        var instance = new VulkanInstance(api, vkInstance, version, supportsProperty2, isValidationEnabled);
        var physDevice = new VulkanPhysDevice(api, new(shared.Device.PhysicalDeviceHandle));

        var vkDevice = new Device(shared.Device.Handle);
        var device = new VulkanDevice(instance, physDevice, vkDevice, alloc);

        var allocator = new VulkanMemoryAllocator(new(version, api, instance, physDevice, device));


        var queue = device.QueuesManager.GetFirstQueueFromIndex((int)shared.Device.GraphicsQueueFamilyIndex);
        if (queue is null)
            return (null,
                    $"Failed to find first device queue from queue family n°{shared.Device.GraphicsQueueFamilyIndex}");

        var cmdBufferPool = new VulkanCommandBufferPool(api, device, queue, alloc);


        var prop = physDevice.GetProperties();
        var deviceName = SilkMarshal.PtrToString((nint)prop.DeviceName) ?? "N/A";

        return (new()
                   {
                       Api = api,
                       Instance = instance,
                       Callbacks = alloc,
                       PhysDevice = physDevice,
                       Device = device,
                       Allocator = allocator,
                       Pool = cmdBufferPool
                   }, deviceName);
    }

    public void Dispose()
    {
        Allocator.Dispose();
        Pool.Dispose();
        Api.Dispose();
    }

    private static Vk GetApi(IVulkanDevice device) =>
        new(new LamdaNativeContext(name =>
        {
            var deviceApi = device.Instance.GetDeviceProcAddress(device.Handle, name);
            if (deviceApi != nint.Zero)
                return deviceApi;
            var instanceApi = device.Instance.GetInstanceProcAddress(device.Instance.Handle, name);

            return instanceApi != nint.Zero ? instanceApi : device.Instance.GetInstanceProcAddress(nint.Zero, name);
        }));
}