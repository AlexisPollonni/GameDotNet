using Arch.Core;
using GameDotNet.Core.Components;

namespace GameDotNet.Core.Abstractions;

public interface ISceneInstance : IDisposable
{
    string Name { get; }
    Identifiable Identifier { get; }
    
    World EntityWorld { get; }
}