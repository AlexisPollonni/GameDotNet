using GameDotNet.Core.Graphics.Vulkan;

namespace GameDotNet.Core.Graphics;

public record struct RenderMesh(Mesh Mesh)
{
    public Mesh Mesh = Mesh;
    public VulkanBuffer? RenderBuffer { get; set; }
}