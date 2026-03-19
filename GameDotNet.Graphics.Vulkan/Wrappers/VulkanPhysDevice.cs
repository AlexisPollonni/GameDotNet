using GameDotNet.Core.Tooling.Extensions;
using GameDotNet.Graphics.Vulkan.Abstractions;
using Silk.NET.Vulkan;

namespace GameDotNet.Graphics.Vulkan.Wrappers;

public class VulkanPhysDevice(IVulkanContext context, PhysicalDevice device)
    : IVulkanWrapper<PhysicalDevice>
{
    public static implicit operator PhysicalDevice(VulkanPhysDevice device) => device.Underlying;

    public PhysicalDeviceFeatures GetFeatures() =>
        Context.Api.GetPhysicalDeviceFeatures(Underlying);

    public PhysicalDeviceProperties GetProperties() =>
        Context.Api.GetPhysicalDeviceProperties(Underlying);

    public PhysicalDeviceMemoryProperties GetMemoryProperties() =>
        Context.Api.GetPhysicalDeviceMemoryProperties(Underlying);

    public unsafe IReadOnlyList<QueueFamilyProperties> GetQueueFamilyProperties()
    {
        var count = 0u;
        Context.Api.GetPhysicalDeviceQueueFamilyProperties(Underlying, ref count, null);

        var properties = new QueueFamilyProperties[count];
        Context.Api.GetPhysicalDeviceQueueFamilyProperties(
            Underlying,
            count.AsSpan(),
            properties.AsSpan()
        );

        return properties;
    }

    public unsafe IReadOnlyList<QueueFamilyProperties2> GetQueueFamilyProperties2()
    {
        var count = 0u;
        Context.Api.GetPhysicalDeviceQueueFamilyProperties2(Underlying, ref count, null);

        var properties = new QueueFamilyProperties2[count];

        //Workaround to avoid using zeroing default constructor
        var prop = new QueueFamilyProperties2(StructureType.QueueFamilyProperties2);
        Array.Fill(properties, prop);

        Context.Api.GetPhysicalDeviceQueueFamilyProperties2(
            Underlying,
            count.AsSpan(),
            properties.AsSpan()
        );

        return properties;
    }

    public IVulkanContext Context => context;
    public PhysicalDevice Underlying => device;
}
