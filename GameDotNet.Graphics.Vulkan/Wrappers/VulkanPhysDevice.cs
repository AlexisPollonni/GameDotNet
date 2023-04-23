using GameDotNet.Core.Tools.Extensions;
using Silk.NET.Vulkan;

namespace GameDotNet.Graphics.Vulkan.Wrappers;

public class VulkanPhysDevice
{
    private readonly Vk _api;
    private readonly PhysicalDevice _handle;

    public VulkanPhysDevice(Vk api, PhysicalDevice handle)
    {
        _api = api;
        _handle = handle;
    }
    
    public static implicit operator PhysicalDevice(VulkanPhysDevice device) => device._handle;

    public  PhysicalDeviceFeatures GetFeatures() => _api.GetPhysicalDeviceFeatures(_handle);

    public  PhysicalDeviceProperties GetProperties() => _api.GetPhysicalDeviceProperties(_handle);

    public  PhysicalDeviceMemoryProperties GetMemoryProperties() => _api.GetPhysicalDeviceMemoryProperties(_handle);

    public unsafe IReadOnlyList<QueueFamilyProperties> GetQueueFamilyProperties()
    {
        var count = 0u;
        _api.GetPhysicalDeviceQueueFamilyProperties(_handle, ref count, null);

        var properties = new QueueFamilyProperties[count];
        _api.GetPhysicalDeviceQueueFamilyProperties(_handle, count.AsSpan(), properties.AsSpan());

        return properties;
    }
}