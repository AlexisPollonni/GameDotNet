using Silk.NET.Core;
using Silk.NET.Vulkan;

namespace Core;

public static class Constants
{
    public static Version32 EngineVersion => Vk.MakeVersion(0, 0, 1);
    public static string EngineName => "GamesDotNet";
}