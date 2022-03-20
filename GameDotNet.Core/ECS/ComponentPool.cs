using GameDotNet.Core.Tools.Containers;
using GameDotNet.Core.Tools.Extensions;

namespace GameDotNet.Core.ECS;

public struct ComponentPool<T> where T : IComponent
{
    public const int DefaultSparsedCapacity = 100,
                     DefaultPackedCapacity = 20;

    public int Count => _packedComponents.Count;

    private struct ComponentHolder
    {
        public EntityId EntityId;
        public T Data;
    }

    private readonly List<int> _sparseEntities;
    private RefStructList<ComponentHolder> _packedComponents;


    public ComponentPool() : this(DefaultSparsedCapacity, DefaultPackedCapacity)
    { }

    public ComponentPool(int sparseCapacity, int packedCapacity)
    {
        _sparseEntities = new(sparseCapacity);
        _packedComponents = new(packedCapacity);
    }

    public void Add(in EntityId id, in T component)
    {
        if (_sparseEntities.Count >= id.Index)
            _sparseEntities.Resize(id.Index + 1, -1);

        _sparseEntities[id.Index] = _packedComponents.Count;
        _packedComponents.Add(new() { EntityId = id, Data = component });
    }

    public void Add(in EntityId id)
    {
        Add(id, default!);
    }

    public void Remove(in EntityId id)
    {
        var i = _sparseEntities[id.Index];
        if (i is -1)
            throw new
                IndexOutOfRangeException($"Can't remove component with id {id} from pool of type {typeof(T).Name}, entity not found");

        _sparseEntities[id.Index] = -1;
        _packedComponents[i] = _packedComponents[^1];
        _packedComponents.RemoveAt(_packedComponents.Count - 1);
    }

    public ref T Get(in EntityId id)
    {
        var i = _sparseEntities[id.Index];
        return ref _packedComponents[i].Data;
    }

    public bool Has(in EntityId id)
    {
        var i = _sparseEntities[id.Index];
        return i >= 0;
    }

    public EntityId[] GetFull()
    {
        var ids = new EntityId[Count];
        var i = 0;
        foreach (ref readonly var holder in _packedComponents.AsReadOnlySpan())
        {
            ids[i] = holder.EntityId;
            i++;
        }

        return ids;
    }
}