using System.Numerics;

namespace GameDotNet.Core.Physics.Components;

public record struct Scale(Vector3 Value)
{
    public Scale() : this(Vector3.One)
    { }

    public static implicit operator Vector3(in Scale s) => s.Value;
    public static implicit operator Scale(in Vector3 v) => new(v);
}