using GameDotNet.Core.Tools.Containers;

namespace GameDotNet.Core.ECS;

public abstract class ComponentStoreBase : IComponentStore
{
    public abstract ref RefStructList<T> GetList<T>() where T : struct, IComponent;

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
}