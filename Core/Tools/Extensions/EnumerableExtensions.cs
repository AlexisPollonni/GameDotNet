using System.Runtime.CompilerServices;

namespace Core.Tools.Extensions;

public static class EnumerableExtensions
{
    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> source) =>
        source.Where(arg => arg is not null)!;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<(T Item, int Index)> WithIndex<T>(this IEnumerable<T> source) =>
        source.Select((item, i) => (item, i));
}