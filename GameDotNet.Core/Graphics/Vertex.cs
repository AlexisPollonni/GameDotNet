using System.Drawing;
using System.Numerics;
using GameDotNet.Core.Tools.Extensions;
using Silk.NET.Vulkan;

namespace GameDotNet.Core.Graphics;

public struct VertexInputDescription
{
    public List<VertexInputBindingDescription> Bindings;
    public List<VertexInputAttributeDescription> Attributes;

    public uint Flags = 0;

    public VertexInputDescription()
    {
        Bindings = new();
        Attributes = new();
    }
}

public struct Vertex
{
    public Vector3 Position
    {
        get => _position;
        set => _position = value;
    }

    public Vector3 Normal
    {
        get => _normal;
        set => _normal = value;
    }

    public Color Color
    {
        get => _color.ToColor();
        set => _color = value.ToVector4();
    }

    private Vector3 _position;
    private Vector3 _normal;
    private Vector4 _color = Vector4.Zero;


    public Vertex(Vector3 position, Vector3 normal, Color color)
    {
        _position = position;
        _normal = normal;
        Color = color;
    }

    public Vertex(Vector3 position, Vector3 normal, Vector4 color)
    {
        _position = position;
        _normal = normal;
        _color = color;
    }
}