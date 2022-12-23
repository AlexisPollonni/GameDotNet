namespace GameDotNet.Core.ECS.Components;

public record struct Tag(string Name)
{
    public string Name = Name;
}