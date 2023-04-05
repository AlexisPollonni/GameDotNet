using System.Numerics;

namespace GameDotNet.Core.Physics.Components;

public record struct Scale(Vector3 Value)
{
    public Scale() : this(Vector3.One)
    { }

    public static implicit operator Vector3(Scale s) => s.Value;
}