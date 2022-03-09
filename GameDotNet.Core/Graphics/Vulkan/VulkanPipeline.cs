using Silk.NET.Vulkan;

namespace GameDotNet.Core.Graphics.Vulkan;

public class VulkanPipeline
{
    private readonly Pipeline _pipeline;

    public VulkanPipeline(Pipeline pipeline)
    {
        _pipeline = pipeline;
    }

    public static implicit operator Pipeline(VulkanPipeline pipeline) => pipeline._pipeline;
}