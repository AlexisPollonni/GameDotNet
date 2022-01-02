using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Silk.NET.Core.Native;

namespace Core.Tools.Extensions;

public static class MemoryExtensions
{
    public static Span<T> ToSpan<T>(ref this T t) where T : struct
    {
        return MemoryMarshal.CreateSpan(ref t, 1);
    }

    public static GlobalMemory ToGlobalMemory<T>(this T s) where T : unmanaged
    {
        var ptr = Marshal.AllocHGlobal(Marshal.SizeOf<T>());
        Marshal.StructureToPtr(s, ptr, false);

        return SilkMarshal.HGlobalToMemory(ptr, Marshal.SizeOf<T>());
    }

    public static GlobalMemory ToGlobalMemory<T>(this IEnumerable<T> enumerable) where T : unmanaged
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

    public static unsafe IEnumerable<string> FromPtrStrArray(byte** ppArray, uint count)
    {
        var array = new string[count];

        for (var i = 0; i < count; i++)
        {
            var p = Marshal.ReadIntPtr((nint)ppArray, i * Marshal.SizeOf<nint>());
            var str = Marshal.PtrToStringAnsi(p);
            array[i] = str ?? string.Empty;
        }

        return array;
    }

    public static unsafe byte** ToPtrStrArray(this IEnumerable<string> source)
    {
        var array = source.ToArray();
        var pArray = Marshal.AllocHGlobal(array.Length * Marshal.SizeOf<nint>());
        var arrayPtr = array.Select(Marshal.StringToHGlobalAnsi).ToArray();

        Marshal.Copy(arrayPtr, 0, pArray, array.Length);

        return (byte**)pArray;
    }

    public static unsafe void FreePtrStrArray(byte** ppArray, uint count)
    {
        for (var i = 0; i < count; i++)
        {
            var ptr = Marshal.ReadIntPtr((IntPtr)ppArray, i * Marshal.SizeOf<nint>());
            Marshal.FreeHGlobal(ptr);
        }

        Marshal.FreeHGlobal((IntPtr)ppArray);
    }
}