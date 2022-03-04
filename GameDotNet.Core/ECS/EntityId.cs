namespace GameDotNet.Core.ECS;

public readonly struct EntityId
{
    public ulong Id { get; init; }
    public uint Reuse { get; init; }
}