namespace GameDotNet.Core.ECS.Components;

public struct Tag : IComponent
{
    public string Name;

    public Tag(string name)
    {
        Name = name;
    }
}