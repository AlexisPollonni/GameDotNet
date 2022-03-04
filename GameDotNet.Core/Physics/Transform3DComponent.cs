using System.Numerics;
using GameDotNet.Core.ECS;

namespace GameDotNet.Core.Physics;

public struct Transform3DComponent : IComponent
{
    public Vector3 Position { get; set; }
    public Vector3 Rotation { get; set; }
    public Vector3 Scale { get; set; }
}