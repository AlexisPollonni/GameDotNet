namespace GameDotNet.Core.ECS;

public abstract class SystemBase
{
    protected readonly World World;
    protected Entity[] Entities { get; private set; }

    protected SystemBase(World world)
    {
        World = world;
        Entities = Array.Empty<Entity>();
    }

    public void RefreshEntities()
    {
        var ids = GetBoundEntities();

        Entities = new Entity[ids.Length];
        var i = 0;
        foreach (ref readonly var id in ids)
        {
            Entities[i] = World.EntityManager.Get(id);
            i++;
        }
    }

    public virtual bool Initialize() => true;
    public abstract void Update(TimeSpan delta);

    protected abstract ReadOnlySpan<EntityId> GetBoundEntities();
}