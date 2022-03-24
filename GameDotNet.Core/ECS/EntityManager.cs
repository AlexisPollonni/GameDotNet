namespace GameDotNet.Core.ECS;

public class EntityManager
{
    private readonly List<EntityId> _entities;
    private readonly Queue<EntityId> _deleted;
    private readonly World _world;

    public EntityManager(World world)
    {
        _world = world;

        _entities = new();
        _deleted = new();
    }

    public Entity Get(in EntityId id)
    {
        if (id.Index >= _entities.Count)
            throw new ArgumentOutOfRangeException(nameof(id), "Entity id is not present in manager");

        if (_entities[id.Index].Version != id.Version)
            throw new ArgumentOutOfRangeException(nameof(id),
                                                  "Manager contains entity at same index with different version, it's possible collection change while accessing");

        return new(_world.ComponentStore, id);
    }

    public Entity CreateEntity()
    {
        EntityId newId;
        if (_deleted.TryDequeue(out var oldId))
        {
            newId = new(oldId.Index, oldId.Version + 1);
            _entities[newId.Index] = newId;
        }
        else
        {
            newId = new(_entities.Count, 0);
            _entities.Add(newId);
        }

        return new(_world.ComponentStore, newId);
    }
}