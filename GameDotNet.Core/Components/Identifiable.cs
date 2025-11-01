using GameDotNet.Core.Abstractions;
using Vogen;

namespace GameDotNet.Core.Components;

/// <summary>
/// For entities that can be identified uniquely. Useful when persisting entities.
/// </summary>
[ValueObject<Guid>]
public readonly partial record struct Identifiable : ISceneComponent;