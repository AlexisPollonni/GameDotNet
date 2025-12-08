using System.Collections.Concurrent;
using System.Reactive;
using Arch.Core;
using GameDotNet.Core.Abstractions;
using GameDotNet.Core.Components;
using GameDotNet.Core.Tooling.Extensions;
using MessagePipe;
using Nito.Disposables;
using Shouldly;

namespace GameDotNet.Core.Services;


public readonly record struct SceneInstantiatedEvent(ISceneInstance Instance);
public readonly record struct SceneDestroyingEvent(ISceneInstance SceneInstance);


/// <summary>
/// Manages the currently loaded scene, modify in the future to enable streaming
/// </summary>
public sealed class SceneInstanceManager(IEnumerable<IAssetImporter<ISceneAsset>> sceneImporters, 
                                        IPublisher<SceneInstantiatedEvent> sceneInstantiatedPublisher,
                                        IPublisher<SceneDestroyingEvent> sceneDestroyingPublisher)
{
    public ISceneInstance? ActiveScene { get; private set; }

    public IReadOnlyCollection<ISceneInstance> EnabledScenes => _enabledScenes;
    public IReadOnlyCollection<ISceneInstance> DisabledScenes => _disabledScenes;
    public IReadOnlyCollection<ISceneInstance> AllInstancedScenes => _allInstancedScenes;
    
    
    private readonly ConcurrentBag<ISceneInstance> _enabledScenes = [];
    private readonly ConcurrentBag<ISceneInstance> _disabledScenes = [];
    private readonly ConcurrentBag<ISceneInstance> _allInstancedScenes = [];
    

    public async ValueTask<Result<ISceneInstance>> LoadAndInstantiate(AssetSource source, bool makeActive = true)
    {
        var result = Result.Failure<ISceneInstance>("Failed to load scene, no importer could handle the asset source");
        
        foreach (var importer in sceneImporters)
        {
            var importResult = await importer.ImportAsync(source);

            if (!importResult.TryMatch(out ISceneAsset? asset)) continue;
            
            asset.ShouldNotBeNull();
                
            return Result.Success(InstantiateScene(asset, makeActive));
        }
        
        return result;
    }

    public ISceneInstance InstantiateScene(ISceneAsset sceneAsset, bool makeActive = true)
    {
        //for now we keep it simple and create the scenes here, maybe use DI container in the future?
        var sceneInstance = new DefaultSceneInstance(sceneAsset);
        if (makeActive)
        {
            // Make sure scene is unloaded before making another active
            DestroyActiveScene();
            ActiveScene = sceneInstance;
            _enabledScenes.Add(sceneInstance);
        }
        else
        {
            _disabledScenes.Add(sceneInstance);
        }
        
        _allInstancedScenes.Add(sceneInstance);
        
        sceneInstantiatedPublisher.Publish(new(sceneInstance));
        return sceneInstance;
    }

    public void DestroyActiveScene()
    {
        if (ActiveScene is null) return;
        sceneDestroyingPublisher.Publish(new(ActiveScene));
        ActiveScene?.Dispose();
        
        ActiveScene = null;
    }

    //TODO: move this to assimp asset importer
    // private void CreateFromSceneObject(SceneObject obj, in Transform accTransform)
    // {
    //     var transform = accTransform * obj.Transform;
    //
    //     foreach (var meshes in obj.Meshes.WithIndex())
    //     {
    //         var e = World.Create(Label.From($"{obj.Name}_{meshes.Index}"),
    //             meshes.Item,
    //             transform.ToTranslation(),
    //             transform.ToRotation(),
    //             transform.ToScale());
    //
    //         _loadedSceneEntities.Add(e);
    //     }
    //
    //     foreach (var child in obj.Children)
    //     {
    //         CreateFromSceneObject(child, transform);
    //     }
    // }

}

internal class DefaultSceneInstance : SingleDisposable<Unit>, ISceneInstance
{
    public ISceneAsset SceneAsset { get; }

    public string Name => SceneAsset.Name;

    public Identifiable Identifier => SceneAsset.Identifier;

    public World EntityWorld { get; } = World.Create();

    private readonly Entity _sceneRoot;


    public DefaultSceneInstance(ISceneAsset sceneAsset) : base(Unit.Default)
    {
        SceneAsset = sceneAsset;

        _sceneRoot = sceneAsset.CreateEntity(EntityWorld);
    }

    protected override void Dispose(Unit _)
    {
        _sceneRoot.DestroyWithChildren();
        EntityWorld.Dispose();
    }
}