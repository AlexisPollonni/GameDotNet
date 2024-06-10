using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using Arch.Core;
using Arch.Core.Extensions;
using Arch.Core.Utils;
using Avalonia.ReactiveUI;
using Collections.Pooled;
using DynamicData;
using DynamicData.Binding;
using GameDotNet.Core.Tools.Extensions;
using GameDotNet.Editor.Tools;
using Microsoft.Extensions.ObjectPool;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace GameDotNet.Editor.ViewModels;

public sealed class EntityInspectorViewModel : ViewModelBase
{
    public ICommand RefreshCommand { get; }


    [Reactive] public ReadOnlyObservableCollection<ComponentNodeViewModel>? Components { get; set; }

    private readonly SourceList<ComponentNodeViewModel> _components;
    private readonly PropertyNodeCache _propertyCache;
    private readonly DefaultObjectPool<PropertyNodeViewModel> _nodePool;
    private EntityReference _selectedEntity;


    public EntityInspectorViewModel(EntityTreeViewModel treeView, EditorUiUpdateSystem uiUpdateSystem)
    {
        _components = new();
        _propertyCache = new();
        _nodePool = new(new NodePooledObjectPolicy(_propertyCache), 10000);
        _selectedEntity = EntityReference.Null;

        this.WhenActivated(d =>
        {
            treeView.SelectedItems.ToObservableChangeSet()
                .ObserveOn(Scheduler.Default)
                .SubscribeMany(node =>
                {
                    UpdateComponents(node.Key);
                    return Disposable.Empty;
                })
                .Subscribe().DisposeWith(d);

            _components.Connect()
                .ObserveOn(AvaloniaScheduler.Instance)
                .Bind(out var comps)
                .Subscribe()
                .DisposeWith(d);
            Components = comps;

            uiUpdateSystem.SampledUpdate.Subscribe(args => UpdateInspector()).DisposeWith(d);
        });

        RefreshCommand = ReactiveCommand.Create(UpdateInspector);
    }


    private void UpdateComponents(EntityReference nodeKey)
    {
        _selectedEntity = nodeKey;
        var entity = nodeKey.Entity;
        var types = entity.GetComponentTypes();

        var nodes = types.Select(type => ComputeNodesFromComponent(entity, type));

        _components.Edit(list =>
        {
            list.Clear();
            list.AddRange(nodes);
        });
    }

    private ComponentNodeViewModel ComputeNodesFromComponent(Entity entity, ComponentType type)
    {
        using var queue = new PooledQueue<PropertyNodeViewModel>();

        var rootNode = new ComponentNodeViewModel(_propertyCache, entity, type);

        queue.Enqueue(rootNode);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            foreach (var entry in current.PropertyTypeEntries)
            {
                if (current.Value is null) continue;

                var value = entry.Getter?.Invoke(current.Value);

                var node = _nodePool.Get();
                
                node.Parent = current;
                node.Name = entry.Info.Name;
                node.Type = entry.Info.PropertyType;
                node.Value = value;
                node.IsDirty = false;

                current.ChildPropertyNodes.Add(node);
                queue.Enqueue(node);
            }

            var childrenItems = TryGetChildrenFromCollectionNode(current);
            if (childrenItems is not null)
            {
                current.ChildItemNodes ??= new();
                current.ChildItemNodes.Edit(list =>
                {
                    foreach (var item in childrenItems)
                    {
                        list.Add(item); //enables unique enumeration and avoids addrange() to array allocs
                        queue.Enqueue(item);
                    }
                });
            }
        }

        return rootNode;
    }

    private IEnumerable<PropertyNodeViewModel>? TryGetChildrenFromCollectionNode(PropertyNodeViewModel node)
    {
        var nodeValue = node.Value;
        if (nodeValue is not IEnumerable source) return null;

        IEnumerable<PropertyNodeViewModel> enumerable;

        if (node.Value is IDictionary dict)
        {
            enumerable = dict.Cast<DictionaryEntry>()
                .Select(e =>
                {
                    var n = _nodePool.Get();
                    n.Parent = node;
                    n.Name = e.Key.ToString();
                    n.Type = e.Value?.GetType() ?? typeof(object);
                    n.Value = e.Value;

                    return n;
                });
        }
        else
        {
            enumerable = source.Cast<object?>()
                .Select((x, i) =>
                {
                    var n = _nodePool.Get();
                    n.Parent = node;
                    n.Name = i.ToString();
                    n.Type = x?.GetType() ?? typeof(object);
                    n.Value = x;
                    return n;
                });
        }

        return enumerable;
    }

    private void UpdateInspector()
    {
        if (Components is null) return;
        if (_selectedEntity == EntityReference.Null)
            return;


        _components.Edit(list =>
        {
            var entity = _selectedEntity.Entity;

            var newCompTypes = entity.GetComponentTypes();
            var oldCompTypes = list.Select(x => x.ComponentType);

            var areCompChanged = !oldCompTypes.SequenceEqual(newCompTypes);

            if (areCompChanged)
            {
                var removedComponents = oldCompTypes.Except(newCompTypes);

                foreach (var component in removedComponents)
                {
                    var i = oldCompTypes.IndexOf(component);
                    list.RemoveAt(i);
                }
            }

            using var queue = list.Cast<PropertyNodeViewModel>().ToPooledQueue();

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                if (current.Parent is not null)
                {
                    var entry = current.Parent.PropertyTypeEntries.First(e => e.Info.Name == current.Name);
                    current.Value = entry.Getter!.Invoke(current.Parent.Value!);
                }
                else
                {
                    var component = (ComponentNodeViewModel)current;

                    component.Value = component.ParentEntity.Get(component.ComponentType);
                }

                queue.EnqueueRange(current.ChildPropertyNodes.Items);

                if (current.ChildItemNodes is not null)
                    current.ChildItemNodes.Edit(list =>
                    {
                        var nodeChildren = TryGetChildrenFromCollectionNode(current);
                        
                        //Bruteforce turns out to be faster, TODO: restrict to visible only nodes
                        foreach (var n in list) _nodePool.Return(n);
                        list.Clear();
                        
                        foreach(var n in nodeChildren) list.Add(n);
                    });
            }


            if (areCompChanged)
            {
                var addedComponents = newCompTypes.Except(oldCompTypes);

                var newComps = addedComponents.Select(c => ComputeNodesFromComponent(entity, c));

                list.AddRange(newComps);
            }
        });
    }

    private static void EditDiffedPooled<T>(SourceList<T> src, IEnumerable<T> items) where T : notnull
    {
        src.Edit(list =>
        {
            using var originalItemsSet = new PooledSet<T>(list);
            using var newItemsSet = new PooledSet<T>(items);

            originalItemsSet.ExceptWith(newItemsSet);
            newItemsSet.ExceptWith(list);

            list.Remove(originalItemsSet);
            list.AddRange(newItemsSet);
        });
    }

    private class NodePooledObjectPolicy(PropertyNodeCache cache) : PooledObjectPolicy<PropertyNodeViewModel>
    {
        public override PropertyNodeViewModel Create() => new(cache);

        public override bool Return(PropertyNodeViewModel obj) => obj.TryReset();
    }
}