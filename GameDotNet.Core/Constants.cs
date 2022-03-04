using Silk.NET.Core;
using Silk.NET.Vulkan;

namespace GameDotNet.Core;

public static class Constants
{
    public const string EngineName = "GamesDotNet";
    public static Version32 EngineVersion => Vk.MakeVersion(0, 0, 1);

    public static string LogsDirectoryPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GamesDotNet", "Logs");

    public static string[] DefaultValidationLayers => new[] { "VK_LAYER_KHRONOS_validation" };
}