using Buffer = Silk.NET.Vulkan.Buffer;

namespace GameDotNet.Core.Graphics;

public struct Mesh
{
    public List<Vertex> Vertices { get; set; }

    internal Buffer Buffer;

    public Mesh(List<Vertex> vertices)
    {
        Vertices = vertices;
        Buffer = new();
    }
}