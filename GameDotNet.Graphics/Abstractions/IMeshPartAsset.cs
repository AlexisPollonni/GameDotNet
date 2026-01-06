using GameDotNet.Core.Abstractions;
using GameDotNet.Graphics.Models;

namespace GameDotNet.Graphics.Abstractions;

public interface IMeshPartAsset : IAsset
{
    IReadOnlyList<Vertex> Vertices { get; }
    IReadOnlyList<uint> Indices { get; }
    IMeshMaterialAsset? Material { get; }
}
