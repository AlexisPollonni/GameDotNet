using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using ReactiveUI;

namespace GameDotNet.Editor.Tools;

[RegisterSingleton(Registration = RegistrationStrategy.Self)]
internal class PropertyNodeCache
{
    private readonly ConcurrentDictionary<Type, PropertyCacheEntry[]> _typeToPropertyCache = [];
    private readonly ConcurrentDictionary<PropertyInfo, PropertyCacheEntry> _infoToCache = [];

    public PropertyCacheEntry GetEntryFromInfo(PropertyInfo info)
    {
        if (_infoToCache.TryGetValue(info, out var entry)) return entry;
        
        Debug.Assert(info.DeclaringType != null, "info.DeclaringType != null");
        
        var entries = GetOrCreatePropertyEntry(info.DeclaringType);
        entry = entries.First(e => e.Info == info);
        
        return entry;
    }

    internal IEnumerable<PropertyCacheEntry> GetDefaultEntries(Type type) =>
        GetOrCreatePropertyEntry(type)
            .Where(entry => entry.Info.GetIndexParameters().Length == 0
                            && entry.Getter is not null
                            && entry.Info.GetMethod!.IsPublic && !entry.Info.IsStatic());

    private PropertyCacheEntry[] GetOrCreatePropertyEntry(Type type)
    {
        return _typeToPropertyCache.GetOrAdd(type, CacheEntryFactory);
    }

    private PropertyCacheEntry[] CacheEntryFactory(Type t)
    {
        return t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                               BindingFlags.Static)
            .Select(info =>
            {
                if (info.IsStatic() || !info.CanRead || info.GetIndexParameters().Length > 0)
                    return new(info, null, null);

                CreateCompiledGetterSetter(info, out var getter, out var setter, info.CanWrite);

                var e = new PropertyCacheEntry(info, getter, setter);
                _infoToCache[info] = e;
                return e;
            })
            .ToArray();
    }

    private static void CreateCompiledGetterSetter(PropertyInfo info, out Func<object, object?> getter, out Action<object, object?>? setter, bool createSetter = false)
    {
        // Define our instance parameter, which will be the input of the Func
        var objParameterExpr = Expression.Parameter(typeof(object), "instance");
        // 1. Cast the instance to the correct type
        var instanceExpr = Expression.Convert(objParameterExpr, info.DeclaringType!);
        // 2. Call the getter and retrieve the value of the property
        var propertyExpr = Expression.Property(instanceExpr, info);
        // 3. Convert the property's value to object
        var propertyObjExpr = Expression.Convert(propertyExpr, typeof(object));
        // Create a lambda expression of the latest call & compile it
        getter = Expression.Lambda<Func<object, object?>>(propertyObjExpr, objParameterExpr)
            .Compile();

        if (!createSetter)
        {
            setter = null;
            return;
        }
        
        var objValueParameterExpr = Expression.Parameter(typeof(object), "value");
        var typedParameterExpr = Expression.Convert(objValueParameterExpr, info.PropertyType);
        var assignExpr = Expression.Assign(propertyExpr, typedParameterExpr);
        setter = Expression
            .Lambda<Action<object, object?>>(assignExpr, objParameterExpr, objValueParameterExpr)
            .Compile();
    }


    internal record PropertyCacheEntry(
        PropertyInfo Info,
        Func<object, object?>? Getter,
        Action<object, object?>? Setter);
}