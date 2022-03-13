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

    public static unsafe VertexInputDescription GetDescription()
    {
        var dummy = new Vertex();
        return new()
        {
            //we will have just 1 vertex buffer binding, with a per-vertex rate
            Bindings = new() { new(0, (uint)sizeof(Vertex), VertexInputRate.Vertex) },

            Attributes = new()
            {
                new(0, 0, Format.R32G32B32Sfloat, (uint)dummy.ByteOffset(ref dummy._position)),
                new(1, 0, Format.R32G32B32Sfloat, (uint)dummy.ByteOffset(ref dummy._normal)),
                new(2, 0, Format.R32G32B32A32Sfloat, (uint)dummy.ByteOffset(ref dummy._color))
            }
        };
    }
}