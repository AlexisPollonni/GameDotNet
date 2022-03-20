using System.Runtime.CompilerServices;

namespace GameDotNet.Core.Tools.Extensions;

public static class EnumerableExtensions
{
    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> source) =>
        source.Where(arg => arg is not null)!;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<(T Item, int Index)> WithIndex<T>(this IEnumerable<T> source) =>
        source.Select((item, i) => (item, i));

    public static void Resize<T>(this List<T> list, int size, T defaultVal)
    {
        var count = list.Count;

        if (size < count)
        {
            list.RemoveRange(size, count - size);
        }
        else if (size > count)
        {
            if (size > list.Capacity)
                list.Capacity = size;
            list.AddRange(Enumerable.Repeat(defaultVal, size - count));
        }
    }

    public static void Resize<T>(this List<T> list, int size) => list.Resize(size, default!);
}