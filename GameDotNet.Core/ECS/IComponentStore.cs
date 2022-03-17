using GameDotNet.Core.Tools.Containers;

namespace GameDotNet.Core.ECS;

public interface IComponentStore
{
    ref RefStructList<T> GetList<T>() where T : struct, IComponent;
}