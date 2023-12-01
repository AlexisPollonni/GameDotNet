using System.Numerics;

namespace GameDotNet.Core.Physics.Components;

public record struct Rotation(Quaternion Value)
{
    public Rotation() : this(Quaternion.Identity)
    { }

    public static implicit operator Quaternion(in Rotation r) => r.Value;
    public static implicit operator Rotation(in Quaternion q) => new(q);
}