using System.Drawing;
using System.Numerics;
using Arch.Core;
using GameDotNet.Core.Abstractions;

namespace GameDotNet.Core.Models;

public readonly record struct EngineStartedEvent;
public readonly record struct EngineStoppingEvent;
public readonly record struct EntityCreatedEvent(Entity New);
public readonly record struct EntityDestroyedEvent(Entity Destroyed);
public readonly record struct EntityComponentAddedEvent(Entity Entity, ComponentType Type);
public readonly record struct EntityComponentSetEvent(Entity Entity, ComponentType Type);
public readonly record struct EntityComponentRemovedEvent(Entity Entity, ComponentType Type);
public readonly record struct ViewportCreatedEvent(IViewPort View);
public readonly record struct ViewportDestroyedEvent(IViewPort Viewport);
public readonly record struct ViewportActiveChangedEvent(IViewPort? Previous, IViewPort Current);
public readonly record struct ViewportResizedEvent(IViewPort Viewport, Size OldSize, Size NewSize);
public readonly record struct ViewportFocusChangedEvent(IViewPort Viewport, bool HasFocus);
public readonly record struct SceneInstantiatedEvent(ISceneInstance Instance);
public readonly record struct SceneDestroyingEvent(ISceneInstance SceneInstance);

public readonly record struct KeyDownEvent(Key Key);
public readonly record struct KeyUpEvent(Key Key);
public readonly record struct MouseClickDownEvent(MouseButton Button);
public readonly record struct MouseClickUpEvent(MouseButton Button);
public readonly record struct MouseScrollEvent(Vector2 Delta);
public readonly record struct MouseMoveEvent(Vector2 Delta);