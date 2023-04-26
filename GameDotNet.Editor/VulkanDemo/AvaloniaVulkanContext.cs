using Avalonia.Vulkan;
using GameDotNet.Graphics.Vulkan.MemoryAllocation;
using GameDotNet.Graphics.Vulkan.Wrappers;
using Silk.NET.Core.Contexts;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace GameDotNet.Editor.VulkanDemo;

internal sealed class AvaloniaVulkanContext : IVulkanContext
{
    public Vk Api { get; }
    public VulkanInstance Instance { get; }
    public VulkanPhysDevice PhysDevice { get; }
    public VulkanDevice Device { get; }
    public VulkanMemoryAllocator Allocator { get; }
    public VulkanCommandBufferPool Pool { get; }

    public static unsafe (AvaloniaVulkanContext? context, string info) TryCreate(IVulkanSharedDevice shared)
    {
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
        var device = new VulkanDevice(instance, physDevice, vkDevice, null);

        var allocator = new VulkanMemoryAllocator(new(version, api, instance, physDevice, device));


        var queue = device.QueuesManager.GetFirstQueueFromIndex((int)shared.Device.GraphicsQueueFamilyIndex);
        if (queue is null)
            return (null,
                    $"Failed to find first device queue from queue family n°{shared.Device.GraphicsQueueFamilyIndex}");

        var cmdBufferPool = new VulkanCommandBufferPool(api, vkDevice, queue);


        var prop = physDevice.GetProperties();
        var deviceName = SilkMarshal.PtrToString((nint)prop.DeviceName) ?? "N/A";

        return (new(api, instance, physDevice, device, allocator, cmdBufferPool), deviceName);
    }

    public void Dispose()
    {
        Allocator.Dispose();
        Pool.Dispose();
        Api.Dispose();
    }

    private AvaloniaVulkanContext(Vk api, VulkanInstance instance, VulkanPhysDevice physDevice, VulkanDevice device,
                                  VulkanMemoryAllocator allocator, VulkanCommandBufferPool pool)
    {
        Api = api;
        Instance = instance;
        PhysDevice = physDevice;
        Device = device;
        Allocator = allocator;
        Pool = pool;
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