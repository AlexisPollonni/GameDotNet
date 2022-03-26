using GameDotNet.Core.ECS.Components;

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

    public ReadOnlySpan<Entity> Get(ReadOnlySpan<EntityId> ids)
    {
        // TODO: Find a better way to do this (array pooling or others)
        var arr = new Entity[ids.Length];
        var i = 0;
        foreach (var id in ids)
        {
            arr[i] = new(_world.ComponentStore, id);
            i++;
        }

        return arr;
    }

    public Entity CreateEntity(string name = "Unnamed")
    {
        EntityId newId;
        if (_deleted.TryDequeue(out var oldId))
        {
            newId = new(oldId.Index, oldId.Version + 1);
            _entities[newId.Index] = newId;
        }
        else
        {
            newId = new(_entities.Count, 1); // New entity versions start at 1
            _entities.Add(newId);
        }

        var entity = new Entity(_world.ComponentStore, newId);

        entity.Add(new Tag(name));
        entity.Add<Parented>();

        return entity;
    }

    public ReadOnlySpan<Entity> CreateEntities(int count)
    {
        var l = new Entity[count];
        for (var i = 0; i < count; i++)
            //TODO: Batch create entities
            l[i] = CreateEntity();

        return l;
    }
}