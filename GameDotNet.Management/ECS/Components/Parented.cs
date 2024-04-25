using Arch.Core;

namespace GameDotNet.Management.ECS.Components;

public readonly record struct ParentEntityComponent(EntityReference Parent);

public readonly record struct ChildrenEntityComponent(List<EntityReference> Children);