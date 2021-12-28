using Silk.NET.Core;
using Silk.NET.Vulkan;

namespace Core;

public static class Constants
{
    public const string EngineName = "GamesDotNet";


    public const string DebugUtilsExtensionName = "VK_EXT_debug_utils";
    public static Version32 EngineVersion => Vk.MakeVersion(0, 0, 1);
    public static string[] DefaultValidationLayers => new[] { "VK_LAYER_KHRONOS_validation" };
}