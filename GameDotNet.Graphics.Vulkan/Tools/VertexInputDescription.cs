using Silk.NET.Vulkan;

namespace GameDotNet.Graphics.Vulkan.Tools;

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