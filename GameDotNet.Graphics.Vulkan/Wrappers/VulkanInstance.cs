using GameDotNet.Core.Tooling;
using GameDotNet.Graphics.Vulkan.Abstractions;
using Nito.Disposables;
using Silk.NET.Core;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;

namespace GameDotNet.Graphics.Vulkan.Wrappers;

public sealed class VulkanInstance(
    IVulkanContext context,
    Instance instance,
    Version32 vkVersion,
    bool supportsProperties2Ext,
    IEnumerable<string> enabledExtensions,
    bool isValidationEnabled = false,
    DebugUtilsMessengerEXT? messenger = null
) : SingleNonblockingDisposable<EmptyStruct>(default), IVulkanWrapper<Instance>
{
    public IVulkanContext Context { get; } = context;
    public Instance Underlying { get; } = instance;
    public bool IsHeadless { get; internal set; }
    public bool SupportsProperties2Ext { get; } = supportsProperties2Ext;
    public IEnumerable<string> EnabledExtensions { get; } = enabledExtensions;
    public bool IsValidationEnabled { get; } = isValidationEnabled;
    public Version32 VkVersion { get; } = vkVersion;

    protected override void Dispose(EmptyStruct context)
    {
        if (messenger is not null)
        {
            Context.Api.TryGetInstanceExtension<ExtDebugUtils>(Context.Instance, out var utils);
            utils.DestroyDebugUtilsMessenger(
                Context.Instance,
                messenger.Value,
                in Context.Callbacks.Handle
            );
        }

        Context.Api.DestroyInstance(Context.Instance, in Context.Callbacks.Handle);
        Context.Api.Dispose();
    }

    public static implicit operator Instance(VulkanInstance instance) => instance.Underlying;

    public IReadOnlyCollection<PhysicalDevice> GetPhysicalDevices() =>
        Context.Api.GetPhysicalDevices(Context.Instance);
}
