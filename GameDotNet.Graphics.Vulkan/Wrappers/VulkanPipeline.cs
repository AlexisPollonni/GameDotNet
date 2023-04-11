using Silk.NET.Vulkan;

namespace GameDotNet.Graphics.Vulkan.Wrappers;

public class VulkanPipeline : IDisposable
{
    public PipelineLayout Layout { get; }

    private readonly Vk _vk;
    private readonly VulkanDevice _device;
    private readonly Pipeline _pipeline;

    public VulkanPipeline(Vk api, VulkanDevice device, Pipeline pipeline, PipelineLayout layout)
    {
        _vk = api;
        _device = device;
        _pipeline = pipeline;
        Layout = layout;
    }

    public static implicit operator Pipeline(VulkanPipeline pipeline) => pipeline._pipeline;

    public void Dispose()
    {
        ReleaseUnmanagedResources();
        GC.SuppressFinalize(this);
    }

    private unsafe void ReleaseUnmanagedResources()
    {
        _vk.DestroyPipeline(_device, _pipeline, null);
        _vk.DestroyPipelineLayout(_device, Layout, null);
    }
}