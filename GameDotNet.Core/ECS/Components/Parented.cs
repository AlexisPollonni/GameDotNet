using Arch.Core;

namespace GameDotNet.Core.ECS.Components;

public readonly record struct ParentEntityComponent(Entity Parent);

public readonly record struct ChildrenEntityComponent(List<Entity> Children);