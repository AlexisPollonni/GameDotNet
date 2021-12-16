using System.Runtime.InteropServices;

namespace Core.Extensions;

public static class MemoryExtensions
{
    public static Span<T> ToSpan<T>(ref this T t) where T : struct
    {
        return MemoryMarshal.CreateSpan(ref t, 1);
    }
}