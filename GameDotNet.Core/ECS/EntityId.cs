namespace GameDotNet.Core.ECS;

public readonly struct EntityId
{
    public EntityId(int index, uint version)
    {
        Index = index;
        Version = version;
    }

    public EntityId(int index) : this()
    {
        Index = index;
    }

    public int Index { get; }

    public uint Version { get; }

    public override string ToString()
    {
        return $"Index = {Index}, Version = {Version}";
    }
}