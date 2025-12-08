using Arch.Core;
using GameDotNet.Core.Components;

namespace GameDotNet.Core.Abstractions;

public interface IAsset
{
    string Name { get; }
    Identifiable Identifier { get; }
    
    Entity CreateEntity(World world);
}