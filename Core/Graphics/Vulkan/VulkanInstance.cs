using Silk.NET.Core;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;

namespace Core.Graphics.Vulkan;

public class VulkanInstance : IDisposable
{
    private readonly DebugUtilsMessengerEXT? _messenger;
    internal readonly Instance Instance;

    internal readonly Vk Vk;
    internal PhysicalDevice? ActivePhysicalDevice;

    internal VulkanInstance(Vk context, Instance instance, Version32 vkVersion, bool supportsProperties2Ext,
                            DebugUtilsMessengerEXT? messenger = null)
    {
        Vk = context;
        Vk.CurrentInstance = instance;
        Instance = instance;
        VkVersion = vkVersion;
        SupportsProperties2Ext = supportsProperties2Ext;
        _messenger = messenger;
    }

    public bool IsHeadless { get; internal set; }
    public bool SupportsProperties2Ext { get; }
    public Version32 VkVersion { get; }

    public void Dispose()
    {
        ReleaseUnmanagedResources();
        GC.SuppressFinalize(this);
    }

    public static implicit operator Instance(VulkanInstance instance) => instance.Instance;

    public IReadOnlyCollection<PhysicalDevice> GetPhysicalDevices() => Vk.GetPhysicalDevices(Instance);

    ~VulkanInstance()
    {
        ReleaseUnmanagedResources();
    }

    private unsafe void ReleaseUnmanagedResources()
    {
        if (_messenger is not null)
        {
            Vk.TryGetInstanceExtension<ExtDebugUtils>(Instance, out var utils);
            utils.DestroyDebugUtilsMessenger(Instance, _messenger.Value, null);
        }

        Vk.DestroyInstance(Instance, null);
    }
}