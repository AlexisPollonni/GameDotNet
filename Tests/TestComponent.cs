using Core.ECS;

namespace Tests;

public struct TestComponent : IComponent
{
    public int Index { get; set; }
}