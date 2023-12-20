using System.Numerics;

namespace GameDotNet.Core.Physics.Components;

public record struct Scale(Vector3 Value)
{
    private Vector3 _value = Value;

    public Vector3 Value
    {
        readonly get => _value;
        set => _value = value;
    }

    public float X
    {
        readonly get => Value.X;
        set => _value.X = value;
    }

    public float Y
    {
        readonly get => Value.Y;
        set => _value.Y = value;
    }

    public float Z
    {
        readonly get => Value.Z;
        set => _value.Z = value;
    }

    public Scale() : this(Vector3.One)
    { }


    public static implicit operator Scale(in Vector3 v) => new(v);
    public static implicit operator Vector3(in Scale s) => s.Value;
}