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

    public static unsafe ulong SizeOf<T>(this IReadOnlyList<T> list) where T : unmanaged
    {
        return (ulong)(sizeof(T) * list.Count);
    }
    
    public static IEnumerable<T> FlattenLevelOrder<T>(
        this IEnumerable<T> items,
        Func<T, IEnumerable<T>?> getChildren)
    {
        var stack = new Queue<T>();
        foreach (var item in items)
            stack.Enqueue(item);

        while (stack.Count > 0)
        {
            var current = stack.Dequeue();
            yield return current;

            var children = getChildren(current);
            if (children is null) continue;

            foreach (var child in children)
                stack.Enqueue(child);
        }
    }
}