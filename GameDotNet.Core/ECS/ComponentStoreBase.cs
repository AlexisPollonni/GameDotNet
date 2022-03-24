using Microsoft.Toolkit.HighPerformance;

namespace GameDotNet.Core.ECS;

public abstract class ComponentStoreBase : IComponentStore
{
    public abstract ref ComponentPool<T> GetPool<T>() where T : struct, IComponent;

    public void Add<T>(in EntityId id) where T : struct, IComponent
        => GetPool<T>().Add(id);

    public void Add<T>(in EntityId id, in T component) where T : struct, IComponent
        => GetPool<T>().Add(id, component);

    public void Remove<T>(in EntityId id) where T : struct, IComponent
        => GetPool<T>().Remove(id);

    public ref T Get<T>(in EntityId id) where T : struct, IComponent
        => ref GetPool<T>().Get(id);

    public bool Has<T>(in EntityId id) where T : struct, IComponent
        => GetPool<T>().Has(id);

    public int Count<T>() where T : struct, IComponent
        => GetPool<T>().Count;

    public ReadOnlySpan<EntityId> GetIdsOf<T>() where T : struct, IComponent
        => GetPool<T>().GetIds();

    public ReadOnlySpan<EntityId> GetIdsOf<T1, T2>()
        where T1 : struct, IComponent
        where T2 : struct, IComponent
    {
        var c1 = Count<T1>();
        var c2 = Count<T2>();

        ReadOnlySpan<EntityId> ids;
        List<EntityId> foundIds;
        if (c1 < c2)
        {
            ids = GetIdsOf<T1>();
            foundIds = new(c1);
            foreach (var id in ids)
                if (Has<T2>(id))
                    foundIds.Add(id);
        }
        else
        {
            ids = GetIdsOf<T2>();
            foundIds = new(c1);
            foreach (var id in ids)
                if (Has<T1>(id))
                    foundIds.Add(id);
        }

        return foundIds.AsSpan();
    }

    public ReadOnlySpan<EntityId> GetIdsOf<T1, T2, T3>()
        where T1 : struct, IComponent
        where T2 : struct, IComponent
        where T3 : struct, IComponent
    {
        var min = Math.Min(Count<T1>(), Math.Min(Count<T2>(), Count<T3>()));

        var foundIds = new List<EntityId>(min);
        if (min == Count<T1>())
        {
            var ids = GetIdsOf<T1>();
            foreach (var id in ids)
                if (Has<T2>(id) && Has<T3>(id))
                    foundIds.Add(id);
        }
        else if (min == Count<T2>())
        {
            var ids = GetIdsOf<T2>();
            foreach (var id in ids)
                if (Has<T1>(id) && Has<T3>(id))
                    foundIds.Add(id);
        }
        else
        {
            var ids = GetIdsOf<T3>();
            foreach (var id in ids)
                if (Has<T1>(id) && Has<T2>(id))
                    foundIds.Add(id);
        }

        return foundIds.AsSpan();
    }

    public ReadOnlySpan<EntityId> GetIdsOf<T1, T2, T3, T4>()
        where T1 : struct, IComponent
        where T2 : struct, IComponent
        where T3 : struct, IComponent
        where T4 : struct, IComponent
    {
        var min = Math.Min(Count<T1>(), Math.Min(Count<T2>(), Math.Min(Count<T3>(), Count<T4>())));

        var foundIds = new List<EntityId>(min);
        if (min == Count<T1>())
        {
            var ids = GetIdsOf<T1>();
            foreach (var id in ids)
                if (Has<T2>(id) && Has<T3>(id) && Has<T4>(id))
                    foundIds.Add(id);
        }
        else if (min == Count<T2>())
        {
            var ids = GetIdsOf<T2>();
            foreach (var id in ids)
                if (Has<T1>(id) && Has<T3>(id) && Has<T4>(id))
                    foundIds.Add(id);
        }
        else if (min == Count<T3>())
        {
            var ids = GetIdsOf<T3>();
            foreach (var id in ids)
                if (Has<T1>(id) && Has<T2>(id) && Has<T4>(id))
                    foundIds.Add(id);
        }
        else
        {
            var ids = GetIdsOf<T4>();
            foreach (var id in ids)
                if (Has<T1>(id) && Has<T2>(id) && Has<T3>(id))
                    foundIds.Add(id);
        }

        return foundIds.AsSpan();
    }
}