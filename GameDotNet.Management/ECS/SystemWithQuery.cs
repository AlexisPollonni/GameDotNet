using Arch.Core;
using Arch.Core.Extensions;
using Arch.Core.Utils;
using Collections.Pooled;
using CommunityToolkit.HighPerformance.Buffers;
using MessagePipe;

namespace GameDotNet.Management.ECS;

public abstract class SystemWithQuery : BeforeAfterSystemBase
{
    public QueryDescription Query { get; }

    private readonly SceneManager _sceneManager;
    private readonly PooledList<Entity> _entities;
    private readonly PooledSet<Entity> _queriedEntities;
    private readonly IDisposable _subDispose;

    protected SystemWithQuery(Universe universe, SceneManager sceneManager, QueryDescription query, SystemDescription description) :
        base(description, universe)
    {
        _sceneManager = sceneManager;
        Query = query;
        _queriedEntities = new();
        _entities = new();
        
        var w = sceneManager.World;
        var q = w.Query(Query);
        
        _subDispose = w.ComponentSet.Subscribe(args =>
        {
            if (!IsRunning) return;
            var currentEntityArchetype = args.Entity.GetArchetype();

            foreach (var archetype in q.GetArchetypeIterator())
            {
                if (archetype == currentEntityArchetype)
                {
                    OnComponentSet(args.Entity, args.Type);
                }
            }
        });
    }

    /// <summary>
    /// Entity that matches the system query was found
    /// </summary>
    /// <param name="entity"></param>
    protected abstract void OnEntityAdded(Entity entity);
    
    /// <summary>
    /// Entity that matches the system query was removed
    /// </summary>
    /// <param name="entity"></param>
    protected abstract void OnEntityRemoved(Entity entity);
    
    /// <summary>
    /// Component in tracked entities was set
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="type"></param>
    protected abstract void OnComponentSet(Entity entity, ComponentType type);
    
    

    public override void BeforeUpdate()
    {
        base.BeforeUpdate();
        
        using var newEntities = GetMatchingEntities(Query);
        
        foreach (var e in newEntities.Span)
        {
            if (!_queriedEntities.Contains(e))
            {
                OnEntityAdded(e);
            }
        }
        
        _queriedEntities.Clear();
        _queriedEntities.UnionWith(newEntities.Span);

        foreach (var e in _entities)
        {
            if (!_queriedEntities.Contains(e))
            {
                OnEntityRemoved(e);
            }
        }
        
        _entities.Clear();
        _entities.AddRange(newEntities.Span);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <remarks>Override <see cref="UpdateAll"/> in subclasses instead for custom update behavior</remarks>
    /// <param name="delta"></param>
    public sealed override void Update(TimeSpan delta)
    {
        UpdateAll(delta, _entities.Span);
    }

    public abstract void UpdateAll(TimeSpan delta, ReadOnlySpan<Entity> entities);

    protected override void Dispose(bool disposing)
    {
        if (!disposing) return;
        
        _subDispose.Dispose();
        _entities.Dispose();
        _queriedEntities.Dispose();
        
        base.Dispose(disposing);
    }

    private SpanOwner<Entity> GetMatchingEntities(in QueryDescription query)
    {
        var matchCount = _sceneManager.World.CountEntities(query);
        var matches = SpanOwner<Entity>.Allocate(matchCount);
        _sceneManager.World.GetEntities(query, matches.Span);

        return matches;
    }
}