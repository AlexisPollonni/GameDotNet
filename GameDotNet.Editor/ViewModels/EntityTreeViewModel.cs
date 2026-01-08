using System.Collections.ObjectModel;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Arch.Core;
using Avalonia.ReactiveUI;
using DynamicData;
using DynamicData.Alias;
using GameDotNet.Core.Abstractions;
using GameDotNet.Core.Models;
using GameDotNet.Core.Services;
using GameDotNet.Core.Tooling.Extensions;
using ReactiveUI.Fody.Helpers;
using EntityNode = DynamicData.Node<GameDotNet.Editor.ViewModels.EntityEntryViewModel, Arch.Core.Entity>;

namespace GameDotNet.Editor.ViewModels;

public sealed class EntityTreeViewModel(
    SceneInstanceManager sceneManager) : ViewModelBase,IEventListener, IAsyncDisposable
{
    [Reactive] public ObservableCollection<EntityNode> SelectedItems { get; set; } = [];

    [Reactive] public ReadOnlyObservableCollection<EntityNode>? EntityTree { get; set; }

    private readonly CancellationTokenSource _cts = new();
    private readonly SourceList<Entity> _cache = new();

    public override void OnActivated(CompositeDisposable disposable)
    {
        base.OnActivated(disposable);
        
        _cache.Connect()
            .ObserveOn(Scheduler.Default)
            .Select(static entity => new EntityEntryViewModel(entity)) //TODO: pool entries? switch to structs?
            .AddKey(static vm => vm.Entity)
            .TransformToTree(static model => model.Parent)
            .ObserveOn(AvaloniaScheduler.Instance)
            .Bind(out var tree)
            .Subscribe()
            .DisposeWith(disposable);

        EntityTree = tree;
    }

    public void Configure(IEventRegistry registry)
    {
        registry.On<EntityCreatedEvent>(OnEntityCreated, _cts.Token);
        registry.On<EntityDestroyedEvent>(OnEntityDestroyed, _cts.Token);

        registry.OnEvent<SceneActiveChangedEvent>(OnActiveSceneChanged, _cts.Token);
    }

    private ValueTask OnActiveSceneChanged(SceneActiveChangedEvent eventArgs, CancellationToken token)
    {
        _cache.Edit(list =>
        {
            list.Clear();
            
            var newActiveScene = eventArgs.Current;
            if (newActiveScene is null) return;
            
            foreach (var arch in newActiveScene.EntityWorld)
            {
                foreach (var chunk in arch)
                {
                    foreach (var i in chunk)
                    {
                        list.Add(chunk.Entity(i));
                    }
                }
            }
        });

        return default;
    }

    private async Task OnEntityCreated(IAsyncEnumerable<EntityCreatedEvent> enumerable, CancellationToken token)
    {
        await foreach (var eventArgs in enumerable
                           .Where(evt => evt.New.World == sceneManager.ActiveScene?.EntityWorld)
                           .WithCancellation(token))
        {
            _cache.Add(eventArgs.New);
        }
    }

    private async Task OnEntityDestroyed(IAsyncEnumerable<EntityDestroyedEvent> enumerable, CancellationToken token)
    {
        await foreach (var eventArgs in enumerable
                           .Where(evt => evt.Destroyed.World == sceneManager.ActiveScene?.EntityWorld)
                           .WithCancellation(token))
        {
            _cache.Add(eventArgs.Destroyed);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        _cts.Dispose();
        
        _cache.Dispose();
        
        Dispose();
    }
}

public record EntityEntryViewModel(Entity Entity)
{
    public Entity Entity { get; } = Entity;
    public Entity Parent => Entity.Parent ?? Entity.Null;
    public string? Name => Entity.Label;
}