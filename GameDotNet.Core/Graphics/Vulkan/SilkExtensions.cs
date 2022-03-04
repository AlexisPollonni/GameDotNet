using Silk.NET.Core.Native;
using Silk.NET.Vulkan;

namespace GameDotNet.Core.Graphics.Vulkan;

public static class SilkExtensions
{
    internal static IEnumerable<GlobalMemory> SetupPNextChain(this IEnumerable<GlobalMemory> nextNodesChain)
    {
        var arr = nextNodesChain.ToArray();
        SetupPNextChain(arr);
        return arr;
    }

    internal static unsafe void SetupPNextChain(params GlobalMemory[] structs)
    {
        if (structs.Length <= 1)
            return;

        for (var i = 0; i < structs.Length - 1; i++)
        {
            structs[i].AsRef<BaseOutStructure>().PNext = structs[i + 1].AsPtr<BaseOutStructure>();
        }
    }

    internal static string? GetLayerName(this LayerProperties properties)
    {
        unsafe
        {
            return SilkMarshal.PtrToString((nint)properties.LayerName);
        }
    }

    internal static string? GetExtensionName(this ExtensionProperties properties)
    {
        unsafe
        {
            return SilkMarshal.PtrToString((nint)properties.ExtensionName);
        }
    }
}