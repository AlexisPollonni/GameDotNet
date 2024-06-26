using Silk.NET.Core;
using Silk.NET.Vulkan;

namespace GameDotNet.Graphics.Vulkan.Tools;

public static class Constants
{
    public static Version32 EngineVersion => Vk.MakeVersion(0, 0, 1);
    public static string[] DefaultValidationLayers => new[] { "VK_LAYER_KHRONOS_validation" };
}