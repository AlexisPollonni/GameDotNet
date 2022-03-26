using System.Runtime.CompilerServices;
using Silk.NET.Vulkan;

namespace GameDotNet.Core.Graphics.Vulkan;

public static class Constants
{
    public static string[] DefaultValidationLayers => new[] { "VK_LAYER_KHRONOS_validation" };
    public static ref readonly AllocationCallbacks NullAlloc => ref Unsafe.NullRef<AllocationCallbacks>();
}