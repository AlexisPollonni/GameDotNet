using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Arch.Core;
using Arch.Core.Extensions;
using Arch.Core.Utils;
using DynamicData;
using DynamicData.Binding;
using Microsoft.Extensions.ObjectPool;
using ReactiveUI;

namespace GameDotNet.Editor.ViewModels;

internal class PropertyNodeCache
{
    private readonly ConcurrentDictionary<Type, PropertyCacheEntry[]> _typeToPropertyCache = [];

    public IEnumerable<PropertyInfo> GetDefaultPropertyInfos(Type type) => GetDefaultEntries(type).Select(e => e.Info);

    public IEnumerable<Func<object, object?>?> GetDefaultPropertyGetters(Type type) =>
        GetDefaultEntries(type).Select(e => e.Getter);

    public IEnumerable<Action<object, object?>?> GetDefaultPropertySetters(Type type) =>
        GetDefaultEntries(type).Select(e => e.Setter);


    internal IEnumerable<PropertyCacheEntry> GetDefaultEntries(Type type) => GetOrCreatePropertyEntry(type)
        .Where(entry => entry.Info.GetIndexParameters().Length == 0
                        && entry.Getter is not null
                        && entry.Info.GetMethod!.IsPublic && !entry.Info.IsStatic());

    private PropertyCacheEntry[] GetOrCreatePropertyEntry(Type type) =>
        _typeToPropertyCache.GetOrAdd(type, static t =>
        {
            return t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic |
                                   BindingFlags.Instance | BindingFlags.Static)
                .Select(info =>
                {
                    if (info.IsStatic() || !info.CanRead || info.GetIndexParameters().Length > 0)
                        return new(info, null, null);

                    // Define our instance parameter, which will be the input of the Func
                    var objParameterExpr = Expression.Parameter(typeof(object), "instance");
                    // 1. Cast the instance to the correct type
                    var instanceExpr = Expression.Convert(objParameterExpr, info.DeclaringType!);
                    // 2. Call the getter and retrieve the value of the property
                    var propertyExpr = Expression.Property(instanceExpr, info);
                    // 3. Convert the property's value to object
                    var propertyObjExpr = Expression.Convert(propertyExpr, typeof(object));
                    // Create a lambda expression of the latest call & compile it
                    var getter = Expression.Lambda<Func<object, object?>>(propertyObjExpr, objParameterExpr)
                        .Compile();

                    if (!info.CanWrite) return new(info, getter, null);

                    var objValueParameterExpr = Expression.Parameter(typeof(object), "value");
                    var typedParameterExpr = Expression.Convert(objValueParameterExpr, info.PropertyType);
                    var assignExpr = Expression.Assign(propertyExpr, typedParameterExpr);
                    var setter = Expression
                        .Lambda<Action<object, object?>>(assignExpr, objParameterExpr,
                            objValueParameterExpr)
                        .Compile();

                    return new PropertyCacheEntry(info, getter, setter);
                }).ToArray();
        });

    internal record PropertyCacheEntry(
        PropertyInfo Info,
        Func<object, object?>? Getter,
        Action<object, object?>? Setter);
}

public class PropertyNodeViewModel : ViewModelBase, IResettable, IEquatable<PropertyNodeViewModel>, IDisposable
{
    public PropertyNodeViewModel? Parent { get; set; }
    public string? Name { get; set; }
    public Type Type { get; set; } = typeof(object);

    public object? Value
    {
        get => _value;
        set => this.RaiseAndSetIfChanged(ref _value, value);
    }

    public bool IsDirty { get; set; } = true;
    public ObservableCollectionExtended<PropertyNodeViewModel> Children { get; } = [];
    public SourceList<PropertyNodeViewModel> ChildPropertyNodes { get; }

    public SourceList<PropertyNodeViewModel>? ChildItemNodes
    {
        get => _childItemNodes;
        set
        {
            if (_childItemNodes is not null && value is null)
            {
                _subscription.Dispose();
                _subscription = ChildPropertyNodes.Connect().Bind(Children).Subscribe();
            }
            else if (_childItemNodes is null && value is not null)
            {
                _subscription.Dispose();
                _subscription = ChildPropertyNodes.Connect().Or(value.Connect()).Bind(Children).Subscribe();
            }

            _childItemNodes = value;
        }
    }


    private readonly PropertyNodeCache _cache;

    private object? _value;
    private IDisposable _subscription;
    private SourceList<PropertyNodeViewModel>? _childItemNodes;

    internal PropertyNodeViewModel(PropertyNodeCache cache)
    {
        _cache = cache;
        ChildPropertyNodes = new();

        _subscription = ChildPropertyNodes.Connect().Bind(Children).Subscribe();
    }

    internal IEnumerable<PropertyNodeCache.PropertyCacheEntry> PropertyTypeEntries => _cache.GetDefaultEntries(Type);


    public bool TryReset()
    {
        Parent = null;
        Name = null;
        Type = typeof(object);
        _value = null;
        IsDirty = true;
        ChildPropertyNodes.Clear();
        ChildItemNodes?.Clear();

        return true;
    }

    public void SetValueWithoutNotification(object? value) => _value = value;

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;

        _subscription.Dispose();
        _childItemNodes?.Dispose();
        ChildPropertyNodes.Dispose();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public bool Equals(PropertyNodeViewModel? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Equals(Parent, other.Parent) && 
               Name == other.Name && 
               Type == other.Type &&
               Equals(_value, other._value);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((PropertyNodeViewModel)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_value, Parent, Name, Type);
    }
}

public sealed class ComponentNodeViewModel : PropertyNodeViewModel
{
    [SetsRequiredMembers]
    internal ComponentNodeViewModel(PropertyNodeCache cache, Entity entity, ComponentType type) : base(cache)
    {
        Parent = null;
        Name = type.Type.Name;
        Type = type;
        Value = entity.Get(type);

        ParentEntity = entity;
        ComponentType = type;
    }

    public Entity ParentEntity { get; }
    public ComponentType ComponentType { get; }
}