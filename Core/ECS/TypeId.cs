using System.Runtime.CompilerServices;

namespace Core.ECS;


public readonly struct TypeId
{
    public Guid Id { get; }
    public string Name { get; }

    private TypeId(Guid id, string name)
    {
        Id = id;
        Name = name;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TypeId Get<T>()
    {
        return new TypeId(typeof(T).GUID, typeof(T).Name);
    }
}

public static class TypeId<T>
{
    public static TypeId Get => TypeId.Get<T>();
}