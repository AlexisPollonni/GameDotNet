namespace GameDotNet.Core.ECS;

public class World
{
    public EntityManager EntityManager { get; }
    public ComponentStoreBase ComponentStore { get; }

    private List<SystemBase> _systems;

    public World(ComponentStoreBase componentStore)
    {
        ComponentStore = componentStore;

        EntityManager = new(this);
        _systems = new();
    }
}