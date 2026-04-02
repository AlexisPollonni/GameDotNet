using System.Runtime.CompilerServices;
using ZLinq;
using ZLinq.Linq;

namespace GameDotNet.Core.Tooling.Extensions;

public static class ValueEnumerableExtensions
{
    //TODO: implement custom IValueEnumerator?
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueEnumerable<
        Zip<FromRepeat<TState>, TEnumerator, TState, TItem, (TState state, TItem item)>,
        (TState state, TItem item)
    > WithState<TEnumerator, TItem, TState>(
        this ValueEnumerable<TEnumerator, TItem> source,
        TState state
    )
        where TEnumerator : struct, IValueEnumerator<TItem>
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
    {
        var stateItems = ValueEnumerable.Repeat(state, int.MaxValue);
        var valueEnumerable = stateItems.Zip(
            source,
            (first, second) => (state: first, item: second)
        );
        return valueEnumerable;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueEnumerable<
        Select<TEnumerator, (TState state, TItem item), TItem>,
        TItem
    > RemoveState<TEnumerator, TItem, TState>(
        this ValueEnumerable<TEnumerator, (TState state, TItem item)> source
    )
        where TEnumerator : struct, IValueEnumerator<(TState state, TItem item)>
#if NET9_0_OR_GREATER
            , allows ref struct
#endif
    {
        return source.Select(static t => t.item);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueEnumerable<
        Zip<
            FromRepeat<TNewState>,
            Select<TEnumerator, (TOldState state, TItem item), TItem>,
            TNewState,
            TItem,
            (TNewState state, TItem item)
        >,
        (TNewState state, TItem item)
    > ReplaceState<TEnumerator, TItem, TOldState, TNewState>(
        this ValueEnumerable<TEnumerator, (TOldState state, TItem item)> source,
        TNewState newState
    )
        where TEnumerator : struct,
            IValueEnumerator<(TOldState state, TItem item)>
#if NET9_0_OR_GREATER
            ,
            allows ref struct
#endif
    {
        return source.RemoveState().WithState(newState);
    }
}
