using System;
using System.Runtime.CompilerServices;
using GameDotNet.Core.ECS;
using GameDotNet.Core.Physics.Components;

namespace GameDotNet.Tests;

internal class StorePrototype : ComponentStoreBase
{
    private ComponentPool<TestComponent> _comp = new();
    private ComponentPool<Translation> _3dComp = new();

    public override ref ComponentPool<T> GetPool<T>()
    {
        if (typeof(T) == typeof(TestComponent))
            return ref Unsafe.As<ComponentPool<TestComponent>, ComponentPool<T>>(ref _comp);

        if (typeof(T) == typeof(Translation))
            return ref Unsafe.As<ComponentPool<Translation>, ComponentPool<T>>(ref _3dComp);
        throw new InvalidOperationException(
                                            $"Type {typeof(T).FullName} was not found in the component store, this shouldn't happen.");
    }
}