using System.Numerics;

namespace GameDotNet.Core.Physics.Components;

public record struct Rotation(Quaternion Value)
{
    private Quaternion _value = Value;

    public Quaternion Value
    {
        readonly get => _value;
        set => _value = value;
    }

    public float X
    {
        readonly get => _value.X;
        set => _value.X = value;
    }

    public float Y
    {
        readonly get => _value.Y;
        set => _value.Y = value;
    }

    public float Z
    {
        readonly get => _value.Z;
        set => _value.Z = value;
    }

    public float W
    {
        readonly get => _value.W;
        set => _value.W = value;
    }

    public Rotation() : this(Quaternion.Identity)
    { }

    public static implicit operator Quaternion(in Rotation r) => r.Value;
    public static implicit operator Rotation(in Quaternion q) => new(q);
}