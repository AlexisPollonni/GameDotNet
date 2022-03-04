using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Toolkit.HighPerformance.Extensions;
using Silk.NET.Core.Native;

namespace GameDotNet.Core.Tools.Extensions;

public static class MemoryExtensions
{
    public static Span<T> AsSpan<T>(ref this T t) where T : unmanaged
    {
        return MemoryMarshal.CreateSpan(ref t, 1);
    }

    public static ref T AsRefOrNull<T>(ref this T? nullable) where T : unmanaged
    {
        return ref nullable is null ? ref Unsafe.NullRef<T>() : ref nullable.DangerousGetValueOrDefaultReference();
    }

    public static ref readonly T AsReadOnlyRefOrNull<T>(in this T? nullable) where T : unmanaged
    {
        if (nullable is null)
            return ref Unsafe.NullRef<T>();

        return ref Unsafe.AsRef(nullable).DangerousGetValueOrDefaultReference();
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