using Arch.Core;

namespace GameDotNet.Core.Abstractions;

public interface IUpdateJob
{
    public ValueTask OnStarted(CancellationToken token = default) =>
        ValueTask.CompletedTask;

    public ValueTask OnStopped(CancellationToken token = default) =>
        ValueTask.CompletedTask;

    public ValueTask OnUpdate(TimeSpan deltaTime, CancellationToken cancellationToken = default);

    public bool IsStarted { get; set; }

    public JobConfiguration Options { get; }
}

public interface IQueryUpdateJob : IUpdateJob
{
    /// <summary>
    /// Arch query description for the system
    /// </summary>
    QueryDescription Query { get; }

    /// <summary>
    /// Entity that matches the system query was found
    /// </summary>
    /// <param name="entity"></param>
    void OnEntityAdded(Entity entity) { }

    /// <summary>
    /// Entity that matches the system query was removed
    /// </summary>
    /// <param name="entity"></param>
    void OnEntityRemoved(Entity entity) { }

    /// <summary>
    /// Component in tracked entities was set
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="type"></param>
    void OnComponentSet(Entity entity, ComponentType type) { }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="deltaTime"></param>
    /// <param name="entities">Entities that currently match the given query</param>
    /// <remarks>Called immediately after standard update, respects dependencies</remarks>
    void OnUpdateQueryEntities(TimeSpan deltaTime, ReadOnlySpan<Entity> entities) { }
}

public interface IJobDependsOn<TDependency> where TDependency : IUpdateJob;

public record JobConfiguration
{
    public TimeSpan UpdateThrottle { get; init; } = TimeSpan.Zero;

    public bool RunsOnMainThread { get; init; } = false;

    public bool StartsWithEngine { get; init; } = true;
}