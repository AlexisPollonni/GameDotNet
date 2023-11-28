using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Arch.Core;
using Arch.Core.Extensions;
using Avalonia.ReactiveUI;
using DynamicData;
using DynamicData.Alias;
using GameDotNet.Management.ECS;
using GameDotNet.Management.ECS.Components;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace GameDotNet.Editor.ViewModels;

public sealed class EntityTreeViewModel : ViewModelBase
{
    [Reactive]
    public ReadOnlyObservableCollection<Node<EntityEntryViewModel, EntityReference>>? EntityTree { get; set; }

    public EntityTreeViewModel(Universe universe)
    {
        var cache = new SourceList<Entity>();

        this.WhenActivated(d =>
        {
            //TODO: Replace with creation events when implemented
            Observable.Interval(TimeSpan.FromMilliseconds(100), Scheduler.Default)
                      .Subscribe(_ =>
                      {
                          var entities = universe.World.Archetypes
                                                 .SelectMany(static arch => arch?.Chunks ?? Enumerable.Empty<Chunk>())
                                                 .SelectMany(static chunk =>
                                                                 chunk.Entities.AsSpan(0, chunk.Size).ToArray());

                          cache.EditDiff(entities);
                      })
                      .DisposeWith(d);

            cache.Connect()
                 .ObserveOn(Scheduler.Default)
                 .AddKey(static entity => entity.Reference())
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
    public EntityReference Parent => !Entity.TryGet(out ParentEntityComponent p) ? EntityReference.Null : p.Parent;
    public string? Name => !Entity.TryGet(out Tag tag) ? null : tag.Name;
}