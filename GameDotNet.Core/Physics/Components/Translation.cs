using System.Numerics;

namespace GameDotNet.Core.Physics.Components;

public record struct Translation(Vector3 Value)
{
    public static implicit operator Vector3(Translation t) => t.Value;
}