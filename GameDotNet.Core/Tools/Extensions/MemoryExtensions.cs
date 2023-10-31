using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using GameDotNet.Core.Tools.Containers;
using Microsoft.Toolkit.HighPerformance;
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

    public static unsafe byte* ToPtr(this string str, ICompositeDisposable d) =>
        str.ToGlobalMemory().DisposeWith(d).AsPtr<byte>();

    public static unsafe byte** ToByteDoublePtr(this IEnumerable<string> str, ICompositeDisposable d)
    {
        return (byte**)str.ToGlobalMemory().DisposeWith(d).AsPtr<byte>();
    }

    public static unsafe T* ToPtrPinned<T>(this T s, ICompositeDisposable dispose) where T : unmanaged
    {
        return new Pinned<T>(in s).DisposeWith(dispose).AsPtr();
    }

    public static unsafe T* AsPtr<T>(this Span<T> span) where T : unmanaged
    {
        return (T*)Unsafe.AsPointer(ref span.GetPinnableReference());
    }
    
    public static unsafe T* AsPtr<T>(ref this T s, ICompositeDisposable dispose) where T : unmanaged
    {
        return new Pinned<T>(in s).DisposeWith(dispose).AsPtr();
    }

    public static unsafe T* AsPtr<T>(ref this T? s, ICompositeDisposable disposable) where T : unmanaged
    {
        ref var r = ref s.AsRefOrNull();
        return Unsafe.IsNullRef(ref r) ? null : new Pinned<T>(in r).DisposeWith(disposable).AsPtr();
    }

    public static unsafe T* ToPtr<T>(this IEnumerable<T> enumerable, ICompositeDisposable dispose) where T : unmanaged
    {
        return (T*)new Memory<T>(enumerable.ToArray()).Pin().DisposeWith(dispose).Pointer;
    }

    /// <summary>
    /// Return a pointer to the first element of a list of unmanaged objects.
    /// WARNING: Adding or removing elements from the structs invalidates the pointer, 
    /// </summary>
    /// <param name="list"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static unsafe T* AsPtr<T>(this List<T> list) where T : unmanaged =>
        (T*)Unsafe.AsPointer(ref list.AsSpan().GetPinnableReference());

    public static unsafe T* AsPtr<T>(this T[]? arr, ICompositeDisposable disposable) where T : unmanaged 
        => (T*)arr.AsMemory().Pin().DisposeWith(disposable).Pointer;

    public static unsafe T* AsPtr<T>(this T[]? arr) where T : unmanaged
        => (T*)Unsafe.AsPointer(ref arr.AsSpan().GetPinnableReference());
    
    

    public static unsafe byte** AsPtr(this string[] arr)
        => (byte**)Unsafe.AsPointer(ref arr.AsSpan().GetPinnableReference());
}