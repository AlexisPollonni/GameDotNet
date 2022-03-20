using System.Numerics;
using GameDotNet.Core.ECS;

namespace GameDotNet.Core.Physics.Components;

public struct Translation : IComponent
{
    public Vector3 Value;

    public Translation(Vector3 value)
    {
        Value = value;
    }
}