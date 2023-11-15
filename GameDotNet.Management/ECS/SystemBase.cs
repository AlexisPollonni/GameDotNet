using System.Diagnostics;
using Arch.Core;

namespace GameDotNet.Management.ECS;

public abstract class SystemBase
{
    public QueryDescription Description { get; }
    public World ParentWorld { get; internal set; }

    internal Stopwatch UpdateWatch { get; }


    protected SystemBase(QueryDescription description)
    {
        UpdateWatch = new();
        Description = description;
    }

    public virtual ValueTask<bool> Initialize(CancellationToken token = default) => ValueTask.FromResult(true);
    public abstract void Update(TimeSpan delta);

    public virtual void OnEntityAdded(Entity entity)
    { }
    
}