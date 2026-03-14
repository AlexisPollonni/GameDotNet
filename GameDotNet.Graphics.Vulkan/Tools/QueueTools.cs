using GameDotNet.Graphics.Vulkan.Wrappers;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace GameDotNet.Graphics.Vulkan.Tools;

internal static class QueueTools
{
    public static int? GetFirstQueueFamilyIndex(IEnumerable<QueueFamilyProperties> families, QueueFlags desiredFlags)
    {
        foreach (var (i, family) in families.Index())
        {
            if (family.QueueFlags.HasFlag(desiredFlags)) return i;
        }

        return null;
    }

    public static int? GetDedicatedQueueFamilyIndex(IEnumerable<QueueFamilyProperties> families,
                                                    QueueFlags desiredFlags, QueueFlags undesiredFlags)
    {
        foreach (var (i, family) in families.Index())
        {
            if (family.QueueFlags.HasFlag(desiredFlags)
                && !family.QueueFlags.HasFlag(undesiredFlags)
                && !family.QueueFlags.HasFlag(QueueFlags.GraphicsBit))
            {
                return i;
            }
        }

        return null;
    }

    public static int? GetSeparateQueueFamilyIndex(IEnumerable<QueueFamilyProperties> families,
                                                   QueueFlags desiredFlags, QueueFlags undesiredFlags)
    {
        int? index = null;
        foreach (var (i, family) in families.Index())
        {
            if (!family.QueueFlags.HasFlag(desiredFlags) || family.QueueFlags.HasFlag(QueueFlags.GraphicsBit))
                continue;

            if (!family.QueueFlags.HasFlag(undesiredFlags))
            {
                return i;
            }

            index = i;
        }

        return index;
    }

    public static int? GetPresentQueueFamilyIndex(VulkanInstance instance, PhysicalDevice device, SurfaceKHR surface,
                                                  IReadOnlyList<QueueFamilyProperties> families)
    {
        if (surface.Handle == 0)
            return null;

        if (!instance.Vk.TryGetInstanceExtension(instance, out KhrSurface ext))
            return null;

        foreach (var (i, _) in families.Index())
        {
            var res = ext.GetPhysicalDeviceSurfaceSupport(device, (uint)i, surface, out var presentSupport);
            if (res != Result.Success)
                return null;

            if (presentSupport)
                return i;
        }

        return null;
    }
}