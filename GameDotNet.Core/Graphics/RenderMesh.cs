using GameDotNet.Core.ECS;

namespace GameDotNet.Core.Graphics;

public struct RenderMesh : IComponent
{
    public Mesh Mesh;

    public RenderMesh(Mesh mesh)
    {
        Mesh = mesh;
    }
}