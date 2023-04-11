using GameDotNet.Graphics.Vulkan.Wrappers;

namespace GameDotNet.Graphics.Vulkan;

public record struct RenderMesh(Mesh Mesh)
{
    public Mesh Mesh = Mesh;
    public VulkanBuffer? RenderBuffer { get; set; }
}