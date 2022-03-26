using GameDotNet.Core.ECS;

namespace GameDotNet.Core.Physics.Components;

public struct Scale : IComponent
{
    public float Value;

    public Scale(float value)
    {
        Value = value;
    }
}