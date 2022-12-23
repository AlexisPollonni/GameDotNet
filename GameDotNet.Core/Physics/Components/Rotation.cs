using System.Numerics;
using GameDotNet.Core.ECS;

namespace GameDotNet.Core.Physics.Components;

public struct Rotation
{
    public Quaternion Value;

    public Rotation(Quaternion value)
    {
        Value = value;
    }
}