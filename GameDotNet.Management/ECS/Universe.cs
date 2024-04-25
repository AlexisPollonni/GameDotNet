using Arch.Core;
using Arch.Core.Extensions;
using Collections.Pooled;
using GameDotNet.Core.Physics.Components;
using GameDotNet.Core.Tools.Extensions;
using GameDotNet.Graphics.Assets;
using GameDotNet.Management.ECS.Components;
using Microsoft.Extensions.Logging;

namespace GameDotNet.Management.ECS;

public class Universe : IDisposable
{
    public World World { get; }

    private readonly ILogger<Universe> _logger;
    private readonly PooledList<SystemBase> _systems;
    private readonly PooledList<EntityReference> _loadedSceneEntities;

    private bool _initialized;
    private Scene? _loadedScene;

    public Universe(ILogger<Universe> logger, IEnumerable<SystemBase> systems)
    {
        _logger = logger;
        World = World.Create();
        
        _systems = new(systems);
        _loadedSceneEntities = new();
    }

    public async Task Initialize(CancellationToken token = default)
    {
        foreach (var system in _systems)
        {
            if (token.IsCancellationRequested) return;
            
            system.ParentWorld = World;
            if (!await system.Initialize(token))
            {
                _logger.LogError("Couldn't initialize system of type {Type}", system.GetType());
                continue;
            }

            system.UpdateWatch.Start();
        }

        _initialized = true;
    }

    public void Update()
    {
        if (!_initialized) return;
        foreach (var system in _systems)
        {
            system.Update(system.UpdateWatch.Elapsed);
            system.UpdateWatch.Restart();
        }
    }

    public bool LoadScene(Scene scene)
    {
        // Make sure scene is unloaded before loading another
        UnloadScene();

        CreateFromSceneObject(scene.Root, new());

        return true;
    }

    public void UnloadScene()
    {
        foreach (ref var entity in _loadedSceneEntities.Span)
            if (entity.IsAlive())
                World.Destroy(entity.Entity);

        _loadedSceneEntities.Clear();
        _loadedScene = null;
    }

    public void Dispose()
    {
        foreach (var system in _systems)
        {
            if (system is IDisposable d)
                d.Dispose();
        }

        _systems.Dispose();
        _loadedSceneEntities.Dispose();
        World.Destroy(World);
    }

    private void CreateFromSceneObject(SceneObject obj, in Transform accTransform)
    {
        var transform = accTransform * obj.Transform;

        foreach (var meshes in obj.Meshes.WithIndex())
        {
            var e = World.Create(new Tag($"{obj.Name}_{meshes.Index}"),
                                 meshes.Item,
                                 transform.ToTranslation(),
                                 transform.ToRotation(),
                                 transform.ToScale());

            _loadedSceneEntities.Add(e.Reference());
        }

        foreach (var child in obj.Children)
        {
            CreateFromSceneObject(child, transform);
        }
    }
}