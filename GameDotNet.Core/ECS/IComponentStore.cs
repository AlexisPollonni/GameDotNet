namespace GameDotNet.Core.ECS;

public interface IComponentStore
{
    public ref T Get<T>(ulong index) where T : struct, IComponent;
    public ulong Add<T>() where T : struct, IComponent;
    public ulong Add<T>(in T component) where T : struct, IComponent;
}