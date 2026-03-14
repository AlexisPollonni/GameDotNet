using System.Collections.Immutable;
using System.Runtime.InteropServices;
using GameDotNet.Core.Tooling.Collections;
using GameDotNet.Core.Tooling.Extensions;
using Serilog;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;

namespace GameDotNet.Graphics.Vulkan.Tools.Extensions;

public static class SilkExtensions
{
    internal static Result LogWarning(
        this Result res,
        string message = "Unexpected Vulkan API error"
    )
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

    public static Result ThrowOnError(
        this Result res,
        string message = "Vulkan API function call failed when it wasn't expected to"
    )
    {
        if (res is Result.Success)
            return res;

        Log.Fatal("{Message} : {VulkanResult}", message, res);
        throw new VulkanException(res);
    }

    internal static IEnumerable<GlobalMemory> SetupPNextChain(
        this IEnumerable<GlobalMemory> nextNodesChain
    )
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

    public static GlobalMemory ToGlobalMemory<T>(this T s)
        where T : unmanaged
    {
        var ptr = Marshal.AllocHGlobal(Marshal.SizeOf<T>());
        Marshal.StructureToPtr(s, ptr, false);

        return SilkMarshal.HGlobalToMemory(ptr, Marshal.SizeOf<T>());
    }

    public static GlobalMemory ToGlobalMemory(this string s)
    {
        return SilkMarshal.StringToMemory(s);
    }

    public static GlobalMemory ToGlobalMemory(this IEnumerable<string> s)
    {
        return SilkMarshal.StringArrayToMemory(s.ToArray());
    }

    public static GlobalMemory ToGlobalMemory<T>(this IEnumerable<T> enumerable)
        where T : unmanaged
    {
        unsafe
        {
            var array = enumerable.ToImmutableArray();
            var mem = GlobalMemory.Allocate(array.Length * sizeof(T));

            for (var i = 0; i < array.Length; i++)
            {
                mem.AsRef<T>(i) = array[i];
            }

            return mem;
        }
    }

    public static unsafe byte* ToPtr(this string str, ICompositeDisposable d) =>
        str.ToGlobalMemory().DisposeWith(d).AsPtr<byte>();

    public static unsafe byte** ToByteDoublePtr(
        this IEnumerable<string> str,
        ICompositeDisposable d
    )
    {
        return (byte**)str.ToGlobalMemory().DisposeWith(d).AsPtr<byte>();
    }
}
