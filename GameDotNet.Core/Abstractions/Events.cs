using Arch.Core;

namespace GameDotNet.Core.Abstractions;

public record struct EngineStartedEvent;
public record struct EngineStoppingEvent;


public record struct EntityCreatedEvent(Entity New);
public record struct EntityDestroyedEvent(Entity Destroyed);
public record struct EntityComponentAddedEvent(Entity Entity, ComponentType Type);
public record struct EntityComponentSetEvent(Entity Entity, ComponentType Type);
public record struct EntityComponentRemovedEvent(Entity Entity, ComponentType Type);