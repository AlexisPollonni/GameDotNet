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
using Collections.Pooled;
using DynamicData;
using DynamicData.Binding;
using GameDotNet.Core.Tools.Extensions;
using GameDotNet.Editor.Tools;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace GameDotNet.Editor.ViewModels;

public sealed class EntityInspectorViewModel : ViewModelBase
{
    public ICommand RefreshCommand { get; }


    [Reactive] public ReadOnlyObservableCollection<ComponentNodeViewModel>? Components { get; set; }

    private readonly SourceList<ComponentNodeViewModel> _components;
    private readonly PropertyNodeCache _propertyCache;
    private EntityReference _selectedEntity;


    public EntityInspectorViewModel(EntityTreeViewModel treeView, EditorUiUpdateSystem uiUpdateSystem)
    {
        _components = new();
        _propertyCache = new();
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

                var node = new PropertyNodeViewModel(_propertyCache)
                {
                    Parent = current,
                    Name = entry.Info.Name,
                    Type = entry.Info.PropertyType,
                    Value = value,
                    IsDirty = false
                };

                current.ChildPropertyNodes.Add(node);
                queue.Enqueue(node);
            }

            using var collectionChildrenItems = GetChildrenFromCollectionNode(current);
            if (collectionChildrenItems is not null)
            {
                current.ChildItemNodes = new();
                current.ChildItemNodes.AddRange(collectionChildrenItems);
                foreach (var item in collectionChildrenItems) 
                    queue.Enqueue(item);
            }
        }

        return rootNode;
    }

    private PooledList<PropertyNodeViewModel>? GetChildrenFromCollectionNode(PropertyNodeViewModel node)
    {
        var nodeValue = node.Value;
        if (nodeValue is not IEnumerable source) return null;

        PooledList<PropertyNodeViewModel> res;
        if (nodeValue is ICollection col)
            res = new(col.Count); //upfront allocate memory if supports Count
        else
            res = new();
        
        IEnumerable<PropertyNodeViewModel> enumerable;

        if (node.Value is IDictionary dict)
        {
            enumerable = from DictionaryEntry entry in dict
                let itemType = entry.Value?.GetType() ?? typeof(object)
                select new PropertyNodeViewModel(_propertyCache)
                {
                    Parent = node,
                    Name = entry.Key.ToString(),
                    Type = itemType,
                    Value = entry.Value
                };
        }
        else
        {
            enumerable = from pair in source.Cast<object?>().WithIndex()
                let itemType = pair.Item.GetType() ?? typeof(object)
                select new PropertyNodeViewModel(_propertyCache)
                {
                    Parent = node,
                    Name = pair.Index.ToString(),
                    Type = itemType,
                    Value = pair.Item
                };
        }

        res.AddRange(enumerable);
        return res;

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

                foreach (var child in current.ChildPropertyNodes.Items)
                    queue.Enqueue(child);

                if (current.ChildItemNodes is not null)
                {
                    using var nodeChildren = GetChildrenFromCollectionNode(current);
                    var newItems = current.ChildItemNodes.Items.Except(nodeChildren!);

                    foreach (var item in newItems) 
                        queue.Enqueue(item);
                    
                    current.ChildItemNodes.EditDiff(nodeChildren!);
                }
            }


            if (areCompChanged)
            {
                var addedComponents = newCompTypes.Except(oldCompTypes);

                var newComps = addedComponents.Select(c => ComputeNodesFromComponent(entity, c));

                list.AddRange(newComps);
            }
        });
    }
}