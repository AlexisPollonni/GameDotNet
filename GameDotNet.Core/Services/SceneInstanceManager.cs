using Arch.Core;
using Arch.Core.Extensions;
using Collections.Pooled;
using GameDotNet.Core.Abstractions;
using GameDotNet.Core.Components;

namespace GameDotNet.Core;

/// <summary>
/// Manages the currently loaded scene, modify in the future to enable streaming
/// </summary>
public sealed class SceneInstanceManager : IDisposable
{
    public World World { get; } = World.Create();
    public ISceneInstance? LoadedScene { get; private set; }
    
    private readonly PooledList<Entity> _loadedSceneEntities = new();

    public bool LoadScene(ISavedSceneAsset scene)
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
                World.Destroy(entity);

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
            var e = World.Create(Label.From($"{obj.Name}_{meshes.Index}"),
                                 meshes.Item,
                                 transform.ToTranslation(),
                                 transform.ToRotation(),
                                 transform.ToScale());

            _loadedSceneEntities.Add(e);
        }

        foreach (var child in obj.Children)
        {
            CreateFromSceneObject(child, transform);
        }
    }
}

internal class DefaultSceneInstance(ISavedSceneAsset sceneAsset) : ISceneInstance
{
    public ISavedSceneAsset SceneAsset { get; } = sceneAsset;
    public void Dispose()
    { }

    public World EntityWorld { get; } = World.Create();
}