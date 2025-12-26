using System.Collections.ObjectModel;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Arch.Core;
using Arch.Core.Extensions;
using Avalonia.ReactiveUI;
using DynamicData;
using DynamicData.Alias;
using GameDotNet.Core;
using GameDotNet.Core.Abstractions;
using GameDotNet.Core.Tooling.Extensions;
using MessagePipe;
using ReactiveUI.Fody.Helpers;
using EntityNode = DynamicData.Node<GameDotNet.Editor.ViewModels.EntityEntryViewModel, Arch.Core.Entity>;

namespace GameDotNet.Editor.ViewModels;

public sealed class EntityTreeViewModel : ViewModelBase
{
    [Reactive] public ObservableCollection<EntityNode> SelectedItems { get; set; }

    [Reactive] public ReadOnlyObservableCollection<EntityNode>? EntityTree { get; set; }

    public EntityTreeViewModel(SceneInstanceManager sceneManager,
        ISubscriber<EntityCreatedEvent> createdEvent,
        ISubscriber<EntityDestroyedEvent> destroyedEvent)
    {
        var cache = new SourceList<Entity>();
        SelectedItems = [];

        this.WhenActivated(d =>
        {
            cache.Edit(list =>
            {
                foreach (var arch in sceneManager.World)
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

            createdEvent.Subscribe(args => cache.Add(args.New)).DisposeWith(d);
            destroyedEvent.Subscribe(args => cache.Remove(args.Destroyed)).DisposeWith(d);

            componentAddedEvent.Subscribe(args => args.)

            cache.Connect()
                .ObserveOn(Scheduler.Default)
                .AddKey(static entity => entity)
                .Select(static entity => new EntityEntryViewModel(entity))
                .TransformToTree(static model => model.Parent)
                .ObserveOn(AvaloniaScheduler.Instance)
                .Bind(out var tree)
                .Subscribe()
                .DisposeWith(d);

            EntityTree = tree;
        });
    }
}

public record EntityEntryViewModel(Entity Entity)
{
    public Entity Entity { get; } = Entity;
    public Entity? Parent => Entity.Parent;
    public string? Name => Entity.Label;
}