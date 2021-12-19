using Silk.NET.Core;
using Silk.NET.Vulkan;

namespace Core.Graphics.Vulkan;

public class VulkanInstance
{
    private PhysicalDevice? _activePhysicalDevice;

    internal VulkanInstance(Instance instance)
    {
        Instance = instance;
    }

    internal Instance Instance { get; }

    unsafe ~VulkanInstance()
    {
        var vk = Vk.GetApi();
        vk.DestroyInstance(Instance, null);
    }

    public IReadOnlyCollection<PhysicalDevice> GetPhysicalDevices() => Vk.GetApi().GetPhysicalDevices(Instance);

    public PhysicalDevice PickPhysicalDeviceForSurface(SurfaceKHR surface)
    {
        var devices = GetPhysicalDevices();

        //TODO: Smarter device picking, currently only picks the first compatible available. Points system ?
        foreach (var physicalDevice in devices)
        {
            if (IsDeviceSuitableForSurface(physicalDevice, surface))
            {
                _activePhysicalDevice = physicalDevice;
                break;
            }
        }

        if (_activePhysicalDevice is null)
            throw new PlatformException("Couldn't find suitable physical device for vulkan surface.");

        return _activePhysicalDevice.Value;
    }

    public bool IsDeviceSuitableForSurface(PhysicalDevice device, SurfaceKHR surface)
    {
        var vk = Vk.GetApi();

        //TODO: Device suitability check

        return true;
    }
}