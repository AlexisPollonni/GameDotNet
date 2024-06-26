using Silk.NET.Core;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;

namespace GameDotNet.Graphics.Vulkan.Wrappers;

public class VulkanInstance : IDisposable
{
    private readonly DebugUtilsMessengerEXT? _messenger;
    internal readonly Instance Instance;

    internal readonly Vk Vk;

    public VulkanInstance(Vk context, Instance instance, Version32 vkVersion, bool supportsProperties2Ext,
                          bool isValidationEnabled = false, DebugUtilsMessengerEXT? messenger = null)
    {
        Vk = context;
        Vk.CurrentInstance = instance;
        Instance = instance;
        VkVersion = vkVersion;
        SupportsProperties2Ext = supportsProperties2Ext;
        IsValidationEnabled = isValidationEnabled;
        _messenger = messenger;
    }

    public bool IsHeadless { get; internal set; }
    public bool SupportsProperties2Ext { get; }
    public bool IsValidationEnabled { get; }
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
        Vk.Dispose();
    }
}