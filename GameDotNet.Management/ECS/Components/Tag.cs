namespace GameDotNet.Management.ECS.Components;

public record struct Tag(string Name)
{
    public string Name = Name;
}