using Core.Tools.Extensions;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace Core.Graphics.Vulkan;

internal static class QueueTools
{
    public static int? GetFirstQueueIndex(IEnumerable<QueueFamilyProperties> families, QueueFlags desiredFlags)
    {
        foreach (var (family, i) in families.WithIndex())
        {
            if (family.QueueFlags.HasFlag(desiredFlags)) return i;
        }

        return null;
    }

    public static int? GetDedicatedQueueIndex(IEnumerable<QueueFamilyProperties> families,
                                              QueueFlags desiredFlags, QueueFlags undesiredFlags)
    {
        foreach (var (family, i) in families.WithIndex())
        {
            if (family.QueueFlags.HasFlag(desiredFlags)
                && !family.QueueFlags.HasFlag(undesiredFlags)
                && !family.QueueFlags.HasFlag(QueueFlags.QueueGraphicsBit))
            {
                return i;
            }
        }

        return null;
    }

    public static int? GetSeparateQueueIndex(IEnumerable<QueueFamilyProperties> families,
                                             QueueFlags desiredFlags, QueueFlags undesiredFlags)
    {
        int? index = null;
        foreach (var (family, i) in families.WithIndex())
        {
            if (!family.QueueFlags.HasFlag(desiredFlags) || family.QueueFlags.HasFlag(QueueFlags.QueueGraphicsBit))
                continue;

            if (!family.QueueFlags.HasFlag(undesiredFlags))
            {
                return i;
            }

            index = i;
        }

        return index;
    }

    public static int? GetPresentQueueIndex(VulkanInstance instance, PhysicalDevice device, SurfaceKHR surface,
                                            IReadOnlyList<QueueFamilyProperties> families)
    {
        if (surface.Handle == 0)
            return null;

        if (!instance.Vk.TryGetInstanceExtension(instance, out KhrSurface ext))
            return null;

        foreach (var (_, i) in families.WithIndex())
        {
            var res = ext.GetPhysicalDeviceSurfaceSupport(device, (uint)i, surface, out var presentSupport);
            if (res != Result.Success)
                return null;

            if (presentSupport == true)
                return i;
        }

        return null;
    }
}