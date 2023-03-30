namespace GameDotNet.Core.Graphics;

public struct Mesh
{
    public List<Vertex> Vertices { get; set; }

    public Mesh(List<Vertex> vertices)
    {
        Vertices = vertices;
    }
}