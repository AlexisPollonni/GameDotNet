using System.Numerics;

namespace GameDotNet.Core.Physics.Components;

public record struct Rotation(Quaternion Value)
{
    public Rotation() : this(Quaternion.Identity)
    { }

    public static implicit operator Quaternion(Rotation r) => r.Value;
}