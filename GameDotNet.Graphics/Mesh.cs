namespace GameDotNet.Graphics;

public sealed class Mesh
{
    public IReadOnlyList<Vertex> Vertices { get; }
    public IReadOnlyList<uint> Indices { get; }

    public Mesh(Vertex[] vertices, uint[] indices)
    {
        Vertices = vertices;
        Indices = indices;
    }
}