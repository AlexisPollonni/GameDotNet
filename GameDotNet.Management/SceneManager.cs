using Arch.Core;
using Arch.Core.Extensions;
using Collections.Pooled;
using GameDotNet.Core.Physics.Components;
using GameDotNet.Core.Tools.Extensions;
using GameDotNet.Graphics.Assets;
using GameDotNet.Management.ECS.Components;

namespace GameDotNet.Management;

/// <summary>
/// Manages the currently loaded scene, modify in the future to enable streaming
/// </summary>
public sealed class SceneManager : IDisposable
{
    public World World { get; }
    public Scene? LoadedScene { get; private set; }
    
    private readonly PooledList<EntityReference> _loadedSceneEntities;

    public SceneManager()
    {
        World = World.Create();
        
        _loadedSceneEntities = new();
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
        LoadedScene = null;
    }

    public void Dispose()
    {
        _loadedSceneEntities.Dispose();
        World.Dispose();
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