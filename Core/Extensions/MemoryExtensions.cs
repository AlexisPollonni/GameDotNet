using System.Runtime.InteropServices;

namespace Core.Extensions;

public static class MemoryExtensions
{
    public static Span<T> ToSpan<T>(ref this T t) where T : struct
    {
        return MemoryMarshal.CreateSpan(ref t, 1);
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