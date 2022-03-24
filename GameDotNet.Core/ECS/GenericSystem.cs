namespace GameDotNet.Core.ECS;

public abstract class GenericSystem<T> : SystemBase
    where T : struct, IComponent
{
    protected GenericSystem(World world) : base(world)
    { }

    protected sealed override ReadOnlySpan<EntityId> GetBoundEntities() => World.ComponentStore.GetIdsOf<T>();
}

public abstract class GenericSystem<T1, T2> : SystemBase
    where T1 : struct, IComponent
    where T2 : struct, IComponent
{
    protected GenericSystem(World world) : base(world)
    { }

    protected sealed override ReadOnlySpan<EntityId> GetBoundEntities() => World.ComponentStore.GetIdsOf<T1, T2>();
}

public abstract class GenericSystem<T1, T2, T3> : SystemBase
    where T1 : struct, IComponent
    where T2 : struct, IComponent
    where T3 : struct, IComponent
{
    protected GenericSystem(World world) : base(world)
    { }

    protected sealed override ReadOnlySpan<EntityId> GetBoundEntities() => World.ComponentStore.GetIdsOf<T1, T2, T3>();
}

public abstract class GenericSystem<T1, T2, T3, T4> : SystemBase
    where T1 : struct, IComponent
    where T2 : struct, IComponent
    where T3 : struct, IComponent
    where T4 : struct, IComponent
{
    protected GenericSystem(World world) : base(world)
    { }

    protected sealed override ReadOnlySpan<EntityId> GetBoundEntities() =>
        World.ComponentStore.GetIdsOf<T1, T2, T3, T4>();
}