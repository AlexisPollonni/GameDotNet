using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace GameDotNet.Core.ECS;

public readonly struct TypeId
{
    public Guid Id { get; }
    public string Name { get; }

    private TypeId(Guid id, string name)
    {
        Id = id;
        Name = name;
    }

    public override string ToString()
    {
        return $"[{Name}] = {Id}";
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static TypeId Get<T>() => TypeIdContainer<T>.Get;

    private static class TypeIdContainer<T>
    {
        [SuppressMessage("ReSharper", "StaticMemberInGenericType")]
        public static TypeId Get { get; } = Generate();

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private static TypeId Generate() =>
            new(typeof(T).GUID, typeof(T).Name);
    }
}