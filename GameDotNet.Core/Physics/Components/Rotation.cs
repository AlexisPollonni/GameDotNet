using System.Numerics;

namespace GameDotNet.Core.Physics.Components;

public struct Rotation
{
    public Quaternion Value;

    public Rotation()
    {
        Value = Quaternion.Identity;
    }

    public Rotation(Quaternion value)
    {
        Value = value;
    }
}