using System;
using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Windows.Input;
using Arch.Core;
using Arch.Core.Extensions;
using Arch.Core.Utils;
using Assimp;
using Collections.Pooled;
using DynamicData;
using DynamicData.Binding;
using GameDotNet.Core.Tools.Extensions;
using GameDotNet.Editor.Tools;
using Microsoft.Toolkit.HighPerformance;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Serilog;

namespace GameDotNet.Editor.ViewModels;

public sealed class EntityInspectorViewModel : ViewModelBase
{
    public ICommand RefreshCommand { get; }



    [Reactive] public ReadOnlyObservableCollection<ComponentNodeViewModel>? Components { get; set; }

    private readonly SourceList<ComponentNodeViewModel> _components;
    private EntityReference _selectedEntity;
    private PropertyNodeCache _propertyCache;


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

            //uiUpdateSystem.SampledUpdate.Subscribe(args => UpdateInspector()).DisposeWith(d);
        });

        RefreshCommand = ReactiveCommand.Create(() =>
        {
            UpdateInspector();
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

    private ComponentNodeViewModel ComputeNodesFromComponent(Entity entity, ComponentType type)
    {
        using var queue = new PooledQueue<PropertyNodeViewModel>();

        var rootNode = new ComponentNodeViewModel(entity, type)
        {
            PropertyGetters = _propertyCache.GetDefaultPropertyGetters(type).ToArray(),
            PropertySetters = _propertyCache.GetDefaultPropertySetters(type).ToArray()
        };

        queue.Enqueue(rootNode);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();


            using var collectionChildrenItems = GetChildrenFromCollectionNode(current);

            foreach (var entry in current.)
            {
                if (current.Value is null) continue;
                var value = entry.Getter?.Invoke(current.Value);

                var node = new PropertyNodeViewModel()
                {
                    Name = entry.Info.Name,
                    Type = entry.Info.PropertyType,
                    Value = value,
                    IsDirty = false,
                    PropertyGetters = _propertyCache.GetDefaultPropertyGetters(type).ToArray(),
                    PropertySetters = _propertyCache.GetDefaultPropertySetters(type).ToArray()
                };

                current.Children.Add(node);
                queue.Enqueue(node);
            }

            if (collectionChildrenItems is not null)
            {
                current.Children.AddRange(collectionChildrenItems);
                foreach (var item in collectionChildrenItems) queue.Enqueue(item);
            }
        }

        return rootNode;
    }

    private static PooledList<PropertyNodeViewModel>? GetChildrenFromCollectionNode(PropertyNodeViewModel node)
    {
        if (node.Value is ICollection col)
        {
            var res = new PooledList<PropertyNodeViewModel>(col.Count);
            IEnumerable<PropertyNodeViewModel> enumerable;

            if (node.Value is IDictionary dict)
            {
                enumerable = from DictionaryEntry entry in dict
                             select new PropertyNodeViewModel(entry.Key.ToString(),
                                                              entry.Value?.GetType() ?? typeof(object),
                                                              entry.Value);

            }
            else
            {
                enumerable = col.Cast<object?>()
                                .Select((o, i) => new PropertyNodeViewModel(i.ToString(),
                                                                            o?.GetType() ?? typeof(object), o));
            }

            res.AddRange(enumerable);
            return res;
        }
        else if (node.Value is IEnumerable enumerable)
            return enumerable.Cast<object?>()
                             .Select((o, i) => new PropertyNodeViewModel(i.ToString(),
                                                                         o?.GetType() ?? typeof(object), o))
                             .ToPooledList();

        return null;
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

                using var queue = ((IExtendedList<PropertyNodeViewModel>)list).ToPooledQueue();

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();



                    if (current is CollectionNodeViewModel colNode)
                    {
                        using var nodeChildren = GetChildrenFromCollectionNode(current);

                        colNode.Items.EditDiff(nodeChildren!);
                    }

                    foreach (var child in current.Children)
                        queue.Enqueue(child);
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

