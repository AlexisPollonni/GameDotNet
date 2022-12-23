using System.Numerics;
using GameDotNet.Core.ECS;

namespace GameDotNet.Core.Physics.Components;

public struct Translation
{
    public Vector3 Value;

    public Translation(Vector3 value)
    {
        Value = value;
    }
}