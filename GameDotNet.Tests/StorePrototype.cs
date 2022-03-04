using System;
using System.Runtime.CompilerServices;
using GameDotNet.Core.ECS;
using GameDotNet.Core.Physics;

namespace GameDotNet.Tests;

internal class StorePrototype : IComponentStore
{
    private RefStructList<TestComponent> _comp = new();
    private RefStructList<Transform3DComponent> _3dComp = new();

    public ulong Add<T>() where T : struct, IComponent
    {
        ref var l = ref GetList<T>();

        var c = new T();
        l.Add(in c);

        return l.Count - 1;
    }

    public ulong Add<T>(in T component) where T : struct, IComponent
    {
        ref var l = ref GetList<T>();

        l.Add(component);
        return l.Count - 1;
    }

    public ref T Get<T>(ulong index) where T : struct, IComponent =>
        ref GetList<T>()[index];

    private ref RefStructList<T> GetList<T>() where T : struct, IComponent
    {
        if (typeof(T) == typeof(TestComponent))
            return ref Unsafe.As<RefStructList<TestComponent>, RefStructList<T>>(ref _comp);

        if (typeof(T) == typeof(Transform3DComponent))
            return ref Unsafe.As<RefStructList<Transform3DComponent>, RefStructList<T>>(ref _3dComp);
        throw new InvalidOperationException(
                                            $"Type {typeof(T).FullName} was not found in the component store, this shouldn't happen.");
    }
}