using Vogen;

namespace GameDotNet.Core.Components;

/// <summary>
/// Provides a simple component to label entities.
/// </summary>
[ValueObject<string>]
public readonly partial record struct Label;