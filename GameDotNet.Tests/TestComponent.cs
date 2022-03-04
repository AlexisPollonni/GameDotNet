using GameDotNet.Core.ECS;

namespace GameDotNet.Tests;

public struct TestComponent : IComponent
{
    public int Index { get; set; }
}