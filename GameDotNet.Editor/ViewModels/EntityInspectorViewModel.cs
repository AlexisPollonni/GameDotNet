using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reflection;
using Arch.Core;
using Arch.Core.Extensions;
using DynamicData;
using DynamicData.Binding;
using GameDotNet.Core.Tools.Extensions;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Serilog;

namespace GameDotNet.Editor.ViewModels;

public sealed class EntityInspectorViewModel : ViewModelBase
{
    [Reactive] public ReadOnlyObservableCollection<PropertyNodeViewModel>? Components { get; set; }

    private readonly SourceList<PropertyNodeViewModel> _components;
    private readonly ConcurrentDictionary<Type, PropertyInfo[]> _typeToPropertyCache;

    public EntityInspectorViewModel(EntityTreeViewModel treeView)
    {
        _components = new();
        _typeToPropertyCache = new();

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
        });
    }

    private PropertyInfo[] GetOrCreatePropertyInfos(Type type) =>
        _typeToPropertyCache.GetOrAdd(type, t => t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic |
                                                                 BindingFlags.Instance | BindingFlags.Static));

    private void UpdateComponents(EntityReference nodeKey)
    {
        var entity = nodeKey.Entity;
        
        var types = entity.GetComponentTypes();

        foreach (var _ in types.Select(type => type.Type)
                               .FlattenLevelOrder(type =>
                               {
                                   if (_typeToPropertyCache.ContainsKey(type)) return Enumerable.Empty<Type>();
                                   return GetOrCreatePropertyInfos(type)
                                          .Select(info => info.PropertyType)
                                          .Where(t => t != type)
                                          .Distinct();
                               }))
        { }

        var components = entity.GetAllComponents();
        
        var nodes = components
                    .Where(o => o is not null)
                    .SelectMany((o, i) => GetPropertyNode(o!, types[i].Type.Name));

        _components.Edit(list =>
        {
            list.Clear();
            list.AddRange(nodes);
        });
    }


    private PropertyNodeViewModel[] GetPropertyNode(object value, string? propertyName)
    {
        var type = value.GetType();
        var properties = GetOrCreatePropertyInfos(type)
                         .Where(info => info.GetIndexParameters().Length == 0 && info.GetMethod is not null &&
                                        info.GetMethod.IsPublic && !info.IsStatic())
                         .Select(info =>
                         {
                             var empty = Enumerable.Empty<PropertyNodeViewModel>();
                             var propValue = info.GetValue(value);

                             if (propValue is null) return null;
                             //To avoid stack overflow
                             if (ReferenceEquals(value, propValue)) return null;
                             
                             return GetPropertyNode(propValue, info.Name).Single();
                         })
                         .WhereNotNull();

        if (value is IEnumerable enumerable)
        {
            var elements = enumerable.Cast<object?>().WhereNotNull().Select((o, i) => GetPropertyNode(o, i.ToString()).Single());

            properties = properties.Concat(elements);
        }

        if (propertyName is null) return properties.ToArray();

        var (getter, setter) = GetPropertyAccessorsFromValue(type, value);
        return [new(propertyName, type, getter, setter, new (properties))];
    }

    //TODO: replace value parameter with expression and entity id
    private static (Func<object> getter, Action<object?> setter) GetPropertyAccessorsFromValue(Type type, object value)
    {
        object DefaultGetter() => value;
        void DefaultSetter(object? v) =>
            Log.Error(new NotImplementedException("Inspector object setters not yet implemented"), "[Inspector] Value = {Value}", value);
        
        if (type.IsPrimitive || type == typeof(string))
        {
            return (DefaultGetter, DefaultSetter);
        }

        // if (TryGetEnumerableType(type, out var _))
        // {
        //     var e = (IEnumerable)value;
        //
        //     var elements = 
        //     return new EnumerablePropertyViewModel(type, properties, elements);
        // }

        return (DefaultGetter, DefaultSetter);
    }

    private static Func<object, object> GenerateGetterLambda(PropertyInfo property)
    {
        // Define our instance parameter, which will be the input of the Func
        var objParameterExpr = Expression.Parameter(typeof(object), "instance");
        // 1. Cast the instance to the correct type
        var instanceExpr = Expression.TypeAs(objParameterExpr, property.DeclaringType);
        // 2. Call the getter and retrieve the value of the property
        var propertyExpr = Expression.Property(instanceExpr, property);
        // 3. Convert the property's value to object
        var propertyObjExpr = Expression.Convert(propertyExpr, typeof(object));
        // Create a lambda expression of the latest call & compile it
        return Expression.Lambda<Func<object, object>>(propertyObjExpr, objParameterExpr).Compile();
    }

    private static bool TryGetEnumerableType(Type type, out Type? elemTypeArg)
    {
        var res = TryGetTypeArgumentsForInterface(type, typeof(IEnumerable<>), out var typeArguments);

        elemTypeArg = typeArguments.FirstOrDefault();

        return res;
    }

    private static bool TryGetTypeArgumentsForInterface(Type type, Type interfaceDef, out IList<Type> typeArgs)
    {
        typeArgs = new List<Type>();
        var typeArguments = type.GetInterfaces()
                                .Append(type)
                                .FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == interfaceDef)
                                ?.GenericTypeArguments ?? Type.EmptyTypes;

        typeArgs.AddRange(typeArguments);
        return typeArguments.Length > 0;
    }
}


public class PropertyNodeViewModel(
    string name,
    Type type,
    Func<object> getter,
    Action<object?> setter,
    ObservableCollection<PropertyNodeViewModel> children) : ViewModelBase
{
    public string Name { get; } = name;
    public Type Type { get; } = type;

    public object? Value
    {
        get => getter();
        set => setter(value);
    }

    public ObservableCollection<PropertyNodeViewModel> Children { get; } = children;
}