using GameDotNet.Core.Tools.Containers;
using GameDotNet.Core.Tools.Extensions;

namespace GameDotNet.Core.ECS;

public struct ComponentPool<T> where T : struct, IComponent
{
    public const int DefaultSparsedCapacity = 100,
                     DefaultPackedCapacity = 20;

    public int Count => _packedComponents.Count;

    private readonly List<int> _sparseEntities;
    private RefStructList<EntityId> _packedEntities;
    private RefStructList<T> _packedComponents;


    public ComponentPool() : this(DefaultSparsedCapacity, DefaultPackedCapacity)
    { }

    public ComponentPool(int sparseCapacity, int packedCapacity)
    {
        _sparseEntities = new(sparseCapacity);
        _packedEntities = new(packedCapacity);
        _packedComponents = new(packedCapacity);
    }

    public void Add(in EntityId id, in T component)
    {
        if (_sparseEntities.Count >= id.Index)
            _sparseEntities.Resize(id.Index + 1, -1);

        _sparseEntities[id.Index] = _packedComponents.Count;
        _packedEntities.Add(id);
        _packedComponents.Add(component);
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

        Swap(id.Index, _packedEntities[^1].Index); // Swap with the last component
        _sparseEntities[id.Index] = -1;

        _packedEntities.RemoveAt(_packedComponents.Count - 1); // Pop the packed lists
        _packedEntities.RemoveAt(_packedEntities.Count - 1);
    }

    public ref T Get(in EntityId id)
    {
        var i = _sparseEntities[id.Index];
        return ref _packedComponents[i];
    }

    public bool Has(in EntityId id)
    {
        var i = _sparseEntities[id.Index];
        return i >= 0;
    }

    public ReadOnlySpan<EntityId> GetIds()
    {
        return _packedEntities.AsReadOnlySpan();
    }

    private void Swap(int firstId, int secondId)
    {
        var i1 = _sparseEntities[firstId];
        var i2 = _sparseEntities[secondId];

        // Deconstruction swap
        (_sparseEntities[firstId], _sparseEntities[secondId]) = (_sparseEntities[secondId], _sparseEntities[firstId]);

        (_packedEntities[i1], _packedEntities[i2]) = (_packedEntities[i2], _packedEntities[i1]);
        (_packedComponents[i1], _packedComponents[i2]) = (_packedComponents[i2], _packedComponents[i1]);
    }
}