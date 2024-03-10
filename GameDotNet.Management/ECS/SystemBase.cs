namespace GameDotNet.Management.ECS;

public abstract class SystemBase
{
    public SystemDescription Description { get; }

    public bool IsInitialized { get; internal set; }

    public bool IsRunning { get; set; }

    protected SystemBase(SystemDescription description)
    {
        Description = description;
    }

    public virtual ValueTask<bool> Initialize(CancellationToken token = default) => ValueTask.FromResult(true);

    public virtual void OnPauseChanged(bool isPaused)
    { }

    public virtual void BeforeUpdate()
    { }

    public abstract void Update(TimeSpan delta);

    public virtual void AfterUpdate()
    { }
}

public abstract class SystemWithQuery : SystemBase
{
    public QueryDescription Query { get; }

    protected SystemWithQuery(Universe universe, QueryDescription query, SystemDescription description) :
        base(universe, description)
    {
        Query = query;
    }

    public sealed override void Update(TimeSpan delta)
    { }

    public abstract void UpdateAll(TimeSpan delta, ReadOnlySpan<Entity> entities);
}

public record SystemDescription(int Priority, bool HasDedicatedThread, bool StartAfterInitialization = true);