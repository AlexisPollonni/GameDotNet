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

    public static GlobalMemory ToGlobalMemory(this string s)
    {
        return SilkMarshal.StringToMemory(s);
    }

    public static GlobalMemory ToGlobalMemory(this IEnumerable<string> s)
    {
        return SilkMarshal.StringArrayToMemory(s.ToArray());
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

    public static unsafe byte** AsByteDoublePtr(this GlobalMemory mem) => (byte**)mem.AsPtr<byte>();
}