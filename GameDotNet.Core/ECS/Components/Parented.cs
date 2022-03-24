namespace GameDotNet.Core.ECS.Components;

public struct Parented : IComponent
{
    public EntityId? Parent;
    public List<EntityId> Children;
}