using Arch.Core;
using AutoFactories;
using GameDotNet.Core.Components;
using GameDotNet.Graphics.Abstractions;
using GameDotNet.Graphics.Models;

namespace GameDotNet.Graphics.Services;

[AutoFactory]
[RegisterTransient<IMeshPartAsset>]
internal sealed class AssimpLoadedMeshPartAsset(
    Identifiable identifier,
    [FromFactory]string name,
    IReadOnlyList<Vertex> vertices,
    IReadOnlyList<uint> indices,
    IMeshMaterialAsset? material
    ) : IMeshPartAsset
{
    public string Name { get; } = name;
    public Identifiable Identifier { get; } = identifier;

    public IReadOnlyList<Vertex> Vertices { get; } = vertices;

    public IReadOnlyList<uint> Indices { get; } = indices;

    public IMeshMaterialAsset? Material { get; } = material;

    public Entity CreateEntity(World world)
    {
        throw new NotImplementedException();
    }
}