﻿namespace GameDotNet.Core.ECS;

public readonly struct Entity
{
    public EntityId Id { get; }

    private readonly ComponentStoreBase _store;

    internal Entity(ComponentStoreBase store, EntityId id)
    {
        Id = id;
        _store = store;
    }

    public Entity Add<T>() where T : struct, IComponent
    {
        _store.Add<T>(Id);

        return this;
    }

    public Entity Add<T>(in T component) where T : struct, IComponent
    {
        _store.Add(Id, component);

        return this;
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