using Silk.NET.Core;
using Silk.NET.Vulkan;

namespace Core.Graphics.Vulkan;

public class InstanceBuilder
{
    public string? ApplicationName { get; set; }
    public string? EngineName { get; set; }

    public Version32? ApplicationVersion { get; set; }
    public Version32? EngineVersion { get; set; }
    public Version32? RequiredApiVersion { get; set; }
    public Version32? DesiredApiVersion { get; set; }

    public IEnumerable<string>? Layers { get; set; }
    public IEnumerable<string>? Extensions { get; set; }

    public DebugUtilsMessengerCallbackFunctionEXT? DebugCallback { get; set; }
    public DebugUtilsMessageSeverityFlagsEXT? DebugMessageSeverity { get; set; }
    public DebugUtilsMessageTypeFlagsEXT? DebugMessageType { get; set; }

    public VulkanInstance Build()
    {
        var vk = Vk.GetApi();

        var apiVersion = Vk.Version10;

        if (RequiredApiVersion > Vk.Version10 || DesiredApiVersion > Vk.Version10)
        {
            var queriedApiVersion = 0U;
            var res = vk.EnumerateInstanceVersion(ref queriedApiVersion);
            if (res != Result.Success && RequiredApiVersion is not null)
                throw new PlatformException("Couldn't find vulkan api version", new VulkanException(res));

            if (queriedApiVersion < RequiredApiVersion)
            { }
        }

        return default;
    }
}