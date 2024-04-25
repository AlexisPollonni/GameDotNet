using System;
using System.Collections.Generic;
using Arch.Core.Utils;
using Arch.Core;
using DynamicData.Binding;
using ReactiveUI;
using Arch.Core.Extensions;
using DynamicData;
using System.Reactive.Linq;
using System.Windows.Input;
using System.Collections.Concurrent;
using System.Reflection;
using System.Linq;
using System.Linq.Expressions;
using System.Diagnostics.CodeAnalysis;

namespace GameDotNet.Editor.ViewModels;

internal class PropertyNodeCache
{
    private readonly ConcurrentDictionary<Type, PropertyCacheEntry[]> _typeToPropertyCache = [];



    public IEnumerable<Func<object, object?>?> GetDefaultPropertyGetters(Type type) => FilterDefaultEntries(type).Select(x => x.Getter);

    public IEnumerable<Action<object, object?>?> GetDefaultPropertySetters(Type type) => FilterDefaultEntries(type).Select(x => x.Setter);


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

    private IEnumerable<PropertyCacheEntry> FilterDefaultEntries(Type type) => GetOrCreatePropertyEntry(type)
        .Where(entry => entry.Info.GetIndexParameters().Length == 0
                                && entry.Getter is not null
                                && entry.Info.GetMethod!.IsPublic && !entry.Info.IsStatic());

    internal record PropertyCacheEntry(
        PropertyInfo Info,
        Func<object, object?>? Getter,
        Action<object, object?>? Setter);
}


public class PropertyNodeViewModel : ViewModelBase
{
    private object? _value;

    public PropertyNodeViewModel()
    {

    }

    public required string? Name { get; init; }
    public required Type Type { get; set; }

    public required object? Value
    {
        get => _value;
        set => this.RaiseAndSetIfChanged(ref _value, value);
    }

    public bool IsDirty { get; set; } = true;

    public required ICollection<Func<object, object?>?> PropertyGetters { get; init; }
    public required ICollection<Action<object, object?>?> PropertySetters { get; init; }

    public ObservableCollectionExtended<PropertyNodeViewModel> Children { get; } = [];


    public void SetValueWithoutNotification(object? value) => _value = value;
}

public sealed class ComponentNodeViewModel : PropertyNodeViewModel
{
    [SetsRequiredMembers]
    public ComponentNodeViewModel(Entity entity, ComponentType type)
    {
        Name = type.Type.Name;
        Type = type;
        Value = entity.Get(type);

        ParentEntity = entity;
        ComponentType = type;
    }

    public Entity ParentEntity { get; }
    public ComponentType ComponentType { get; }
}

public sealed class CollectionNodeViewModel : PropertyNodeViewModel, IDisposable
{
    public SourceList<PropertyNodeViewModel> Properties { get; }
    public SourceList<PropertyNodeViewModel> Items { get; }

    private readonly IDisposable _subscription;

    public CollectionNodeViewModel()
    {
        Properties = new SourceList<PropertyNodeViewModel>();
        Items = new SourceList<PropertyNodeViewModel>();

        _subscription = Properties.Connect().Concat(Items.Connect()).Bind(Children).Subscribe();
    }

    public void Dispose()
    {
        _subscription.Dispose();
    }
}
