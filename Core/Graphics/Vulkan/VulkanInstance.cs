using Core.Extensions;
using Silk.NET.Core;
using Silk.NET.Vulkan;

namespace Core.Graphics.Vulkan;

public class VulkanInstance
{
    internal Instance Instance { get; }

    private PhysicalDevice? _activePhysicalDevice;
    
    internal VulkanInstance(Instance instance)
    {
        Instance = instance;
    }

    public IEnumerable<PhysicalDevice> GetPhysicalDevices()
    {
        unsafe
        {
            var vk = Vk.GetApi();
        
            uint deviceCount = 0;
            var res = vk.EnumeratePhysicalDevices(Instance, ref deviceCount, null);

            if (deviceCount == 0 || res != Result.Success)
                throw new PlatformException("No vulkan supported device were found.", new VulkanException(res));
            
            var devices = new PhysicalDevice[deviceCount];
            res = vk.EnumeratePhysicalDevices(Instance, deviceCount.ToSpan(), devices);
            if (res != Result.Success)
                throw new PlatformException("Failed to query vulkan physical devices.", new VulkanException(res));

            return devices;
        }
    }

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