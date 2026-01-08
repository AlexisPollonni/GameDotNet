using Arch.Core;
using AutoCtor;
using GameDotNet.Core.Components;
using GameDotNet.Graphics.Abstractions;
using GameDotNet.Graphics.Models;

namespace GameDotNet.Graphics.Services;

[AutoConstruct]
internal sealed partial class AssimpLoadedMeshPartAsset : IMeshPartAsset
{
    public string Name { get; }
    public Identifiable Identifier { get; }

    public IReadOnlyList<Vertex> Vertices { get; }

    public IReadOnlyList<uint> Indices { get; }

    public IMeshMaterialAsset? Material { get; }
    
    public Entity CreateEntity(World world)
    {
        throw new NotImplementedException();
    }
}