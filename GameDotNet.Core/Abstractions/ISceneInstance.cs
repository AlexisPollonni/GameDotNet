using Arch.Core;

namespace GameDotNet.Core.Abstractions;

public interface ISceneInstance : IDisposable
{
    World EntityWorld { get; }
}