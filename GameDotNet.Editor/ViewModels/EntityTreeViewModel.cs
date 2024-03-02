using System;
using System.Collections.ObjectModel;
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
using MessagePipe;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using EntityNode = DynamicData.Node<GameDotNet.Editor.ViewModels.EntityEntryViewModel, Arch.Core.EntityReference>;

namespace GameDotNet.Editor.ViewModels;

public sealed class EntityTreeViewModel : ViewModelBase
{
    [Reactive]
    public ObservableCollection<EntityNode> SelectedItems { get; set; }

    [Reactive]
    public ReadOnlyObservableCollection<EntityNode>? EntityTree { get; set; }

    public EntityTreeViewModel(Universe universe)
    {
        var cache = new SourceList<Entity>();
        SelectedItems = new();

        this.WhenActivated(d =>
        {
            cache.Edit(list =>
            {
                foreach (var arch in universe.World)
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

            universe.World.EntityCreated.AsObservable().Subscribe(args => cache.Add(args.Entity)).DisposeWith(d);
            universe.World.EntityDestroyed.AsObservable().Subscribe(args => cache.Remove(args.Entity)).DisposeWith(d);
            
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