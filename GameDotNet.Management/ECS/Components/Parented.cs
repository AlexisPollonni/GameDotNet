using Arch.Core;

namespace GameDotNet.Management.ECS.Components;

public readonly record struct ParentEntityComponent(Entity Parent);

public readonly record struct ChildrenEntityComponent(List<Entity> Children);