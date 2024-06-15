using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reflection;
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


    [Reactive] public ReadOnlyObservableCollection<PropertyNodeViewModel>? Components { get; set; }

    private readonly SourceList<PropertyNodeViewModel> _components;
    private readonly PropertyNodeCache _propertyCache;
    private readonly DefaultObjectPool<PropertyNodeViewModel> _nodePool;
    private EntityReference _selectedEntity;


    public EntityInspectorViewModel(EntityTreeViewModel treeView, EditorUiUpdateSystem uiUpdateSystem)
    {
        _components = new();
        _propertyCache = new();
        _nodePool = new(new NodePooledObjectPolicy(_propertyCache), 10000);
        _selectedEntity = EntityReference.Null;

        var sync = new object();
        this.WhenActivated(d =>
        {
            treeView.SelectedItems.ToObservableChangeSet()
                .ObserveOn(Scheduler.Default)
                .SubscribeMany(node =>
                {
                    lock (sync)
                    {
                        UpdateComponents(node.Key);   
                    }
                    return Disposable.Empty;
                })
                .Subscribe().DisposeWith(d);

            _components.Connect()
                .ObserveOn(AvaloniaScheduler.Instance)
                .Bind(out var comps)
                .Subscribe()
                .DisposeWith(d);
            Components = comps;

            uiUpdateSystem.SampledUpdate
                .Subscribe(_ =>
                {
                    lock (sync)
                    {
                        UpdateInspector();
                    }
                })
                .DisposeWith(d);
        });

        RefreshCommand = ReactiveCommand.Create(() =>
        {
            lock (sync)
            {
                UpdateInspector();
            }
        });
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

    private PropertyNodeViewModel ComputeNodesFromComponent(Entity entity, ComponentType type)
    {
        using var queue = new PooledQueue<PropertyNodeViewModel>();

        var rootNode = CreateNode(entity, type);

        queue.Enqueue(rootNode);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            foreach (var entry in current.PropertyTypeEntries)
            {
                if (current.Value is null) continue;
                
                var node = CreateNode(current, entry.Info);

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
                .Select(e => CreateNode(node, e.Key.ToString(), e.Value, true));
        }
        else
        {
            enumerable = source.Cast<object?>()
                .Select((x, i) => CreateNode(node, i.ToString(), x, true));
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
            var oldCompTypes = list.Select(x => (ComponentType)x.Type);

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

            using var queue = list.ToPooledQueue();

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                if (current.Parent is not null)
                {
                    var entry = current.Parent.PropertyTypeEntries.First(e => e.Info.Name == current.Name);
                    current.Value = entry.Getter!.Invoke(current.Parent.Value!);
                }
                else
                    current.Value = _selectedEntity.Entity.Get(current.Type);

                queue.EnqueueRange(current.ChildPropertyNodes.Items
                    .SkipWhile(n => !n.IsVisible)
                    .TakeWhile(n => n.IsVisible));


                // COLLECTION UPDATE
                current.ChildItemNodes?.Edit(list =>
                {
                    var curCount = list.Count;
                    //Short path for collection removals
                    if (current.Value is ICollection col)
                    {
                        var newCount = col.Count;
                        if (newCount < curCount)
                        {
                            foreach (var n in list.Skip(newCount))
                                _nodePool.Return(n);
                            list.RemoveRange(newCount, curCount - newCount);
                            return;
                        }
                    }

                    var c1 = list.TakeWhile(n => !n.IsVisible).Count();
                    if (c1 == curCount) return;

                    var c2 = list.Skip(c1).TakeWhile(n => n.IsVisible).Count();

                    var nodeChildren = TryGetChildrenFromCollectionNode(current)!;

                    var truncatedChildren = nodeChildren
                        .Skip(c1);

                    using var e = truncatedChildren.GetEnumerator();

                    var i = c1;
                    for (; i < c1 + c2; i++)
                    {
                        if (e.MoveNext())
                        {
                            // In the visible window so we swap w new value
                            _nodePool.Return(list[i]);
                            list[i] = e.Current;
                        }
                        else
                            break; // no more new values so exit loop
                    }

                    if (i >= curCount)
                        while (e.MoveNext())
                            list.Add(e.Current); // appends new missing nodes to end of list
                    else if (!e.MoveNext())
                    {
                        for (var j = i; j < curCount; j++)
                            _nodePool.Return(list[j]);
                        list.RemoveRange(i, curCount); // Truncates excess nodes no longer present in list
                    }
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

    private PropertyNodeViewModel CreateNode(PropertyNodeViewModel? parent, string? name, Type type, object? value, bool isReadonly = false)
    {
        var n = _nodePool.Get();

        n.Parent = parent;
        n.Name = name;
        n.Type = type;
        n.Value = value;
        n.IsDirty = false;
        n.IsReadonly = isReadonly;

        return n;
    }

    private PropertyNodeViewModel CreateNode(PropertyNodeViewModel? parent, string? name, object? value, bool isReadonly = false) =>
        CreateNode(parent, name, value?.GetType() ?? typeof(object), value, isReadonly);

    private PropertyNodeViewModel CreateNode(PropertyNodeViewModel parent, PropertyInfo info)
    {
        var entry = _propertyCache.GetEntryFromInfo(info);

        var value = entry.Getter?.Invoke(parent.Value);
        var node = CreateNode(parent, info.Name, info.PropertyType, value);

        node.IsReadonly = !info.CanWrite;
        return node;
    }

    private PropertyNodeViewModel CreateNode(Entity entity, ComponentType type)
    {
        return CreateNode(null, type.Type.Name, type, entity.Get(type));
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