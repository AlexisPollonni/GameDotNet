using System.Runtime.CompilerServices;

namespace GameDotNet.Core.ECS;

public readonly struct Entity
{
    public EntityId Id { get; }

    private readonly ComponentStoreBase _store;

    internal Entity(ComponentStoreBase store, EntityId id)
    {
        Id = id;
        _store = store;
    }

    public ref readonly Entity Add<T>() where T : struct, IComponent
    {
        unsafe
        {
            _store.Add<T>(Id);

            ref readonly var e = ref this;
            var p = Unsafe.AsPointer(ref Unsafe.AsRef(this));

            return ref Unsafe.AsRef<Entity>(p);
        }
    }

    public void Add<T>(in T component) where T : struct, IComponent
    {
        _store.Add(Id, component);
    }

    public void Remove<T>() where T : struct, IComponent
    {
        _store.Remove<T>(Id);
    }

    public ref T Get<T>() where T : struct, IComponent
    {
        ref var c = ref _store.Get<T>(Id);
        return ref c;
    }

    public bool Has<T>() where T : struct, IComponent
    {
        return _store.Has<T>(Id);
    }

    public bool TryGet<T>(out T? component) where T : struct, IComponent
    {
        var has = Has<T>();
        if (has)
        {
            component = _store.Get<T>(Id);
            return has;
        }

        component = default;
        return has;
    }
}