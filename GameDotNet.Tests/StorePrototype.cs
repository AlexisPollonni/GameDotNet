using System;
using System.Runtime.CompilerServices;
using GameDotNet.Core.ECS;
using GameDotNet.Core.Physics;
using GameDotNet.Core.Tools.Containers;

namespace GameDotNet.Tests;

internal class StorePrototype : ComponentStoreBase
{
    private RefStructList<TestComponent> _comp = new();
    private RefStructList<Transform3DComponent> _3dComp = new();

    public override ref RefStructList<T> GetList<T>()
    {
        if (typeof(T) == typeof(TestComponent))
            return ref Unsafe.As<RefStructList<TestComponent>, RefStructList<T>>(ref _comp);

        if (typeof(T) == typeof(Transform3DComponent))
            return ref Unsafe.As<RefStructList<Transform3DComponent>, RefStructList<T>>(ref _3dComp);
        throw new InvalidOperationException(
                                            $"Type {typeof(T).FullName} was not found in the component store, this shouldn't happen.");
    }
}