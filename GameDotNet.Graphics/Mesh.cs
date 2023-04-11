namespace GameDotNet.Graphics;

public class Mesh
{
    public List<Vertex> Vertices { get; set; }

    public Mesh(List<Vertex> vertices)
    {
        Vertices = vertices;
    }
}