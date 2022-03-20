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
}