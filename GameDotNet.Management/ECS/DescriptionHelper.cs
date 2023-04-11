using Arch.Core;
using Arch.Core.Utils;

namespace GameDotNet.Management.ECS;

public static class Query
{
    public static DescriptionBuilder All(params Type[] types) => new DescriptionBuilder().All(types);

    public static DescriptionBuilder Any(params Type[] types) => new DescriptionBuilder().Any(types);

    public static DescriptionBuilder Exclusive(params Type[] types) => new DescriptionBuilder().Exclusive(types);

    public static DescriptionBuilder None(params Type[] types) => new DescriptionBuilder().None(types);

    public static DescriptionBuilder All<T>() => new DescriptionBuilder().All<T>();

    public static DescriptionBuilder Any<T>() => new DescriptionBuilder().Any<T>();

    public static DescriptionBuilder Exclusive<T>() => new DescriptionBuilder().Exclusive<T>();

    public static DescriptionBuilder None<T>() => new DescriptionBuilder().None<T>();
}

public class DescriptionBuilder
{
    private readonly List<Type> _all, _any, _none, _exclusive;

    public DescriptionBuilder()
    {
        _all = new();
        _any = new();
        _none = new();
        _exclusive = new();
    }

    public static implicit operator QueryDescription(DescriptionBuilder b) => b.Build();

    public QueryDescription Build()
    {
        return new()
        {
            All = Convert(_all), Any = Convert(_any), Exclusive = Convert(_exclusive), None = Convert(_none)
        };
    }

    public DescriptionBuilder All(params Type[] types)
    {
        _all.AddRange(types);
        return this;
    }

    public DescriptionBuilder Any(params Type[] types)
    {
        _any.AddRange(types);
        return this;
    }

    public DescriptionBuilder Exclusive(params Type[] types)
    {
        _exclusive.AddRange(types);
        return this;
    }

    public DescriptionBuilder None(params Type[] types)
    {
        _none.AddRange(types);
        return this;
    }

    public DescriptionBuilder All<T>()
    {
        _all.Add(typeof(T));
        return this;
    }

    public DescriptionBuilder Any<T>()
    {
        _any.Add(typeof(T));
        return this;
    }

    public DescriptionBuilder Exclusive<T>()
    {
        _exclusive.Add(typeof(T));
        return this;
    }

    public DescriptionBuilder None<T>()
    {
        _none.Add(typeof(T));
        return this;
    }

    private ComponentType[] Convert(List<Type> types) => types.Distinct().Select(Component.GetComponentType).ToArray();
}