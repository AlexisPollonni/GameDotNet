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

    public virtual ValueTask<bool> Initialize(CancellationToken token = default) =>
        ValueTask.FromResult(true);

    public virtual void OnRunningStatusChanged(bool isRunning) { }

    public abstract void Update(TimeSpan delta);
}

public record SystemDescription(int Priority = 0, bool RunsOnMainThread = false, bool StartAfterInitialization = true)
{
    public TimeSpan UpdateThrottle { get; init; } = TimeSpan.Zero;
}

public abstract class BeforeAfterSystemBase : SystemBase, IDisposable
{
    private readonly Universe _universe;
    private readonly BeforePassSystem _beforePass;
    private readonly AfterPassSystem _afterPass;

    protected BeforeAfterSystemBase(SystemDescription description, Universe universe) : base(description)
    {
        _universe = universe;
        _beforePass = new(description, this);
        _afterPass = new(description, this);
    }

    public virtual void BeforeUpdate() { }
    public virtual void AfterUpdate() { }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;
        
        _universe.RemoveSystem(_beforePass);
        _universe.RemoveSystem(_afterPass);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private sealed class BeforePassSystem(SystemDescription description, BeforeAfterSystemBase parent)
        : SystemBase(description with { Priority = int.MinValue })
    {
        public override void Update(TimeSpan delta)
        {
            parent.BeforeUpdate();
        }
    }

    private sealed class AfterPassSystem(SystemDescription description, BeforeAfterSystemBase parent)
        : SystemBase(description with { Priority = int.MaxValue })
    {
        public override void Update(TimeSpan delta)
        {
            parent.AfterUpdate();
        }
    }
}