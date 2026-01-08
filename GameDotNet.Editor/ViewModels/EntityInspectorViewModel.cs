using System.Collections;
using System.Collections.ObjectModel;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reflection;
using System.Windows.Input;
using Arch.Core;
using Arch.Core.Extensions;
using AutoCtor;
using Avalonia.ReactiveUI;
using Collections.Pooled;
using DynamicData;
using DynamicData.Binding;
using GameDotNet.Core.Abstractions;
using GameDotNet.Core.Models;
using GameDotNet.Core.Tooling.Extensions;
using GameDotNet.Editor.Services;
using GameDotNet.Editor.Tools;
using Microsoft.Extensions.ObjectPool;
using ReactiveUI;
using Shouldly;
using ZLinq;

namespace GameDotNet.Editor.ViewModels;

[AutoConstruct]
[RegisterSingleton(Registration = RegistrationStrategy.Self)]
internal sealed partial class EntityInspectorViewModel : ViewModelBase
{
    public ICommand RefreshCommand { get; private set; }

    public ReadOnlyObservableCollection<PropertyNodeViewModel>? Components { get; set; }
    
    private readonly EntityTreeViewModel _entityTreeView;
    private readonly PropertyNodeCache _propertyCache;
    private readonly ObjectPool<PropertyNodeViewModel> _nodePool;

    private readonly CancellationTokenSource _cts = new();
    private readonly SourceCache<PropertyNodeViewModel, ComponentType> _components = new(model => model.Type);
    private Entity _selectedEntity = Entity.Null;
    private Signature _loadedSignature = Signature.Null;

    [AutoPostConstruct]
    public void Configure(IEventRegistry registry)
    {
        var token = _cts.Token;
        registry.OnEvent<EntityComponentAddedEvent>(OnComponentAdded, token);
        registry.OnEvent<EntityComponentSetEvent>(OnComponentSet, token);
        registry.OnEvent<EntityComponentRemovedEvent>(OnComponentRemoved, token);
        registry.OnEvent<EditorUpdateEventArgs>(OnEditorUpdate, token);
    }

    public override void OnActivated(CompositeDisposable disposable)
    {
        RefreshCommand = ReactiveCommand.Create(OnRefreshCommand).DisposeWith(disposable);
        
        _entityTreeView.SelectedItems
            .ToObservableChangeSet()
            .ObserveOn(Scheduler.Default)
            .FirstAsync()
            .DistinctUntilChanged()
            .OnItemRefreshed(node => OnActiveEntityChanged(node.Key))
            .Subscribe().DisposeWith(disposable);

        _components.Connect()
            .ObserveOn(AvaloniaScheduler.Instance)
            .Bind(out var comps)
            .Subscribe()
            .DisposeWith(disposable);
        Components = comps;
    }

    private ValueTask OnEditorUpdate(EditorUpdateEventArgs arg1, CancellationToken arg2)
    {
        OnRefreshCommand();
        return default;
    }

    private ValueTask OnComponentAdded(EntityComponentAddedEvent args, CancellationToken token)
    {
        var newNode = RebuildNodesFromComponent(args.Entity, args.Type);
        
        _components.AddOrUpdate(newNode);
        _loadedSignature = args.Entity.GetComponentTypes();
        return default;
    }

    private ValueTask OnComponentSet(EntityComponentSetEvent args, CancellationToken token)
    {
        var node = _components.Lookup(args.Type);

        if (node.HasValue)
        {
            var newValue = args.Entity.Get(args.Type);
            UpdateNodeAndChildren(node.Value, newValue);
        }
        else
        {
            var newNode = RebuildNodesFromComponent(args.Entity, args.Type);
            _components.AddOrUpdate(newNode);
        }

        return default;
    }

    private ValueTask OnComponentRemoved(EntityComponentRemovedEvent args, CancellationToken token)
    {
        var node = _components.Lookup(args.Type);

        if (node.HasValue)
        {
            _components.Remove(args.Type);
            ReturnNodeToPool(node.Value);
        }

        return default;
    }

    private void OnActiveEntityChanged(Entity newEntity)
    {
        _loadedSignature = newEntity.GetComponentTypes();
        
        _components.Edit(updater =>
        {
            updater.Clear();
            foreach (var type in _loadedSignature)
            {
                var newNode = RebuildNodesFromComponent(newEntity, type);
                updater.AddOrUpdate(newNode);
            }
        });
        
        _selectedEntity = newEntity;
    }

    private void OnRefreshCommand()
    {
        foreach (var componentNode in _components.Items)
        {
            UpdateNodeAndChildren(componentNode, _selectedEntity.Get(componentNode.Type));
        }
    }

    private PropertyNodeViewModel RebuildNodesFromComponent(Entity entity, ComponentType type)
    {
        var rootNode = CreateNode(entity, type);
        
        RebuildChildrenNodes(rootNode);

        return rootNode;
    }

    private void RebuildChildrenNodes(PropertyNodeViewModel parent)
    {
        foreach (var child in parent.Children)
        {
            ReturnNodeToPool(child);
        }
        
        parent.ChildPropertyNodes.Clear();
        parent.ChildItemNodes?.Clear();

        using var queue = new PooledQueue<PropertyNodeViewModel>([parent]);
        
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            
            queue.EnqueueRange(RebuildNodeProperties(current));
            queue.EnqueueRange(RebuildNodeCollectionItems(current));
        }
    }

    private IEnumerable<PropertyNodeViewModel> RebuildNodeProperties(PropertyNodeViewModel node)
    {
        node.ChildPropertyNodes.Clear();
        if (node.Value is null) return [];
       
        foreach (var entry in node.PropertyTypeEntries)
        {
            var childNode = CreateNode(node, entry.Info).ShouldNotBeNull();

            node.ChildPropertyNodes.Add(childNode);
        }

        return node.ChildPropertyNodes.Items;
    }

    private IEnumerable<PropertyNodeViewModel> RebuildNodeCollectionItems(PropertyNodeViewModel node)
    {
        node.ChildItemNodes?.Clear();
        var childrenItems = TryGetChildrenFromCollectionNode(node);
        if (childrenItems is null) return [];
        
        node.ChildItemNodes ??= new();
        node.ChildItemNodes.AddRange(childrenItems);

        return node.ChildItemNodes.Items;
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
    
    

    /// <summary>
    /// Updates node value and checks if children need to be rebuilt incrementally
    /// </summary>
    /// <param name="node">node to update</param>
    /// <param name="newValue">new node value from getter</param>
    /// <returns>true if structure is rebuilt, false if only value is updated</returns>
    private bool UpdateNodeIncremental(PropertyNodeViewModel node, object? newValue)
    {
        var oldValue = node.Value;
        
        if (oldValue is null && newValue is null) return false;
        if (ReferenceEquals(oldValue, newValue)) return false;

        var needsStructuralUpdate = RequiresChildrenRebuild(oldValue, newValue);

        if (needsStructuralUpdate)
        {
            RebuildChildrenNodes(node);
        }

        if(oldValue is null || !oldValue.Equals(newValue))
        {
            node.Value = newValue;
        }
        return needsStructuralUpdate;
    }
    
    private void UpdateNodeAndChildren(PropertyNodeViewModel node, object? newValue)
    {
        var queue = new PooledQueue<(PropertyNodeViewModel, object?)>([(node, newValue)]);
        
        while (queue.Count > 0)
        {
            var (currentNode, currentNewValue) = queue.Dequeue();

            if (currentNode.Parent is null)
            {
                continue;
            }

            var needsStructuralUpdate = UpdateNodeIncremental(currentNode, currentNewValue);

            //If structural update was needed, children are already rebuilt from scratch, valid for:
            // - enumerable that do not implement collection
            // - when underlying type changed
            // - when collection size changed
            // - or when going from null to non-null and vice versa
            if (needsStructuralUpdate || currentNewValue is null)
            {
                continue; // children already rebuilt
            }

            //Iterate over all children properties and update their values
            foreach (var propEntry in currentNode.PropertyTypeEntries)
            {
                var childToQueue = currentNode.ChildPropertyNodes.Items.FirstOrDefault(childNode =>
                    childNode.Name == propEntry.Info.Name &&
                    childNode.Type == propEntry.Info.PropertyType);

                if (childToQueue is null)
                {
                    currentNode.ChildPropertyNodes.Add(CreateNode(currentNode, propEntry.Info).ShouldNotBeNull());
                    continue;
                }

                var childNewValue = propEntry.Getter?.Invoke(currentNewValue);
                queue.Enqueue((childToQueue, childNewValue));
            }

            if (currentNewValue is not ICollection newValueCollection) continue;
            //Iterate over all collection items and queues their values for update
            if (currentNode.ChildItemNodes is null)
            {
                RebuildNodeCollectionItems(currentNode);
                continue;
            }
                
            var newItems = newValueCollection.AsValueEnumerable<object?>();
            var currentChildItems = currentNode.ChildItemNodes.Items.AsValueEnumerable();
                
            foreach (var childItemsToQueueForUpdate in currentChildItems.Zip(newItems, (childItemNode, childItemValue) => (childItemNode, childItemValue)))
            {
                queue.Enqueue(childItemsToQueueForUpdate);
            }
        }
    }
    
    private bool RequiresChildrenRebuild(object? oldValue, object? newValue)
    {
        if (oldValue is null && newValue is null) return false;
        if (oldValue is null || newValue is null) return true;

        var oldType = oldValue.GetType();
        var newType = newValue.GetType();

        if (oldType != newType) return true;

        if (oldValue is ICollection oldCol && newValue is ICollection newCol)
            return oldCol.Count != newCol.Count;
        
        if(oldValue is IEnumerable || newValue is IEnumerable)
            return true; // different enumerables require rebuild

        return false;
    }

    private void ReturnNodeToPool(PropertyNodeViewModel node)
    {
        // BFS to return all descendants
        using var queue = new PooledQueue<PropertyNodeViewModel>();
        queue.Enqueue(node);
    
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            
            queue.EnqueueRange(current.Children);
        
            _nodePool.Return(current);
        }
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

    private PropertyNodeViewModel? CreateNode(PropertyNodeViewModel parent, PropertyInfo info)
    {
        if(parent.Value is null) return null;
        
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
}