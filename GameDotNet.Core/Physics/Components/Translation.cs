using System.Numerics;

namespace GameDotNet.Core.Physics.Components;

public record struct Translation(Vector3 Value)
{
    public static implicit operator Vector3(in Translation t) => t.Value;
    public static implicit operator Translation(in Vector3 v) => new(v);
}