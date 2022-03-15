using Serilog;
using Silk.NET.Core.Contexts;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;

namespace GameDotNet.Core.Graphics.Vulkan;

public static class SilkExtensions
{
    internal static Result LogWarning(this Result res, string message = "Unexpected Vulkan API error")
    {
        if (res is not Result.Success)
            Log.Warning("{Message} : {VulkanResult}", message, res);

        return res;
    }

    internal static Result LogError(this Result res, string message = "Unexpected Vulkan API error")
    {
        if (res is not Result.Success)
            Log.Error("{Message} : {VulkanResult}", message, res);

        return res;
    }

    internal static Result ThrowOnError(this Result res,
                                        string message = "Vulkan API function call failed when it wasn't expected to")
    {
        if (res is Result.Success)
            return res;

        Log.Fatal("{Message} : {VulkanResult}", message, res);
        throw new VulkanException(res);
    }

    internal static IEnumerable<GlobalMemory> SetupPNextChain(this IEnumerable<GlobalMemory> nextNodesChain)
    {
        var arr = nextNodesChain.ToArray();
        SetupPNextChain(arr);
        return arr;
    }

    internal static bool IsGlfw(this IView view) => view.Native?.Kind.HasFlag(NativeWindowFlags.Glfw) ?? false;


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