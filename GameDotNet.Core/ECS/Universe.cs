using System.Numerics;
using Arch.Core;
using Arch.Core.Extensions;
using Collections.Pooled;
using GameDotNet.Core.ECS.Components;
using GameDotNet.Core.Physics.Components;
using GameDotNet.Core.Tools;
using GameDotNet.Core.Tools.Extensions;
using Serilog;

namespace GameDotNet.Core.ECS;

public class Universe : IDisposable
{
    public World World { get; }
    private readonly PooledList<SystemBase> _systems;
    private readonly PooledList<EntityReference> _loadedSceneEntities;

    private Scene? _loadedScene;

    public Universe()
    {
        World = World.Create();
        _systems = new();
        _loadedSceneEntities = new();
    }

    public void Initialize()
    {
        foreach (var system in _systems)
        {
            if (!system.Initialize())
            {
                Log.Error("Couldn't initialize system of type {Type}", system.GetType());
                continue;
            }

            system.UpdateWatch.Start();
        }
    }

    public void Update()
    {
        foreach (var system in _systems)
        {
            system.Update(system.UpdateWatch.Elapsed);
            system.UpdateWatch.Restart();
        }
    }

    public void RegisterSystem<T>(T system) where T : SystemBase
    {
        //TODO: Replace with dependency injection
        system.ParentWorld = World;
        _systems.Add(system);
    }

    public bool LoadScene(Scene scene)
    {
        // Make sure scene is unloaded before loading another
        UnloadScene();

        CreateFromSceneObject(scene.Root, Matrix4x4.Identity);

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

    private void CreateFromSceneObject(SceneObject obj, Matrix4x4 accTransform)
    {
        var transform = accTransform * obj.Transform;

        if (!Matrix4x4.Decompose(transform, out var scale, out var rotation, out var translation))
        {
            Log.Error("<Scene> Scene object {ObjectName} has invalid transform matrix, ignoring it", obj.Name);

            Matrix4x4.Decompose(accTransform, out scale, out rotation, out translation);
        }

        foreach (var meshes in obj.Meshes.WithIndex())
        {
            var e = World.Create(new Tag($"{obj.Name}_{meshes.Index}"),
                                 meshes.Item,
                                 new Translation(translation),
                                 new Rotation(rotation),
                                 new Scale(scale));

            _loadedSceneEntities.Add(e.Reference());
        }

        foreach (var child in obj.Children)
        {
            CreateFromSceneObject(child, transform);
        }
    }
}