namespace GameDotNet.Core.ECS;

public interface IComponentStore
{
    ref ComponentPool<T> GetPool<T>() where T : struct, IComponent;
}