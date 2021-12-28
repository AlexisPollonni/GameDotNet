using System.Runtime.InteropServices;
using Silk.NET.Vulkan;

namespace Core.Graphics.Vulkan;

public static class Extensions
{
    internal static string? GetLayerName(this LayerProperties properties)
    {
        unsafe
        {
            return Marshal.PtrToStringAnsi((nint)properties.LayerName);
        }
    }

    internal static string? GetExtensionName(this ExtensionProperties properties)
    {
        unsafe
        {
            return Marshal.PtrToStringAnsi((nint)properties.ExtensionName);
        }
    }
}