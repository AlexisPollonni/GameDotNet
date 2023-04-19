using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Rendering.Composition;
using GpuInterop.VulkanDemo;

namespace GameDotNet.Editor.VulkanDemo;

public class VulkanDemoControl : DrawingSurfaceDemoBase
{
    class VulkanResources : IAsyncDisposable
    {
        public VulkanContext Context { get; }
        public VulkanSwapchain Swapchain { get; }
        public VulkanContent Content { get; }

        public VulkanResources(VulkanContext context, VulkanSwapchain swapchain, VulkanContent content)
        {
            Context = context;
            Swapchain = swapchain;
            Content = content;
        }

        public async ValueTask DisposeAsync()
        {
            Context.Pool.FreeUsedCommandBuffers();
            Content.Dispose();
            await Swapchain.DisposeAsync();
            Context.Dispose();
        }
    }

    private VulkanResources? _resources;

    protected override (bool success, string info) InitializeGraphicsResources(Compositor compositor,
                                                                               CompositionDrawingSurface
                                                                                   compositionDrawingSurface,
                                                                               ICompositionGpuInterop gpuInterop)
    {
        var (context, info) = VulkanContext.TryCreate(gpuInterop);
        if (context == null)
            return (false, info);
        try
        {
            var content = new VulkanContent(context);
            _resources = new(context, new(context, gpuInterop, compositionDrawingSurface), content);
            return (true, info);
        }
        catch (Exception e)
        {
            return (false, e.ToString());
        }
    }

    protected override void FreeGraphicsResources()
    {
        _resources?.DisposeAsync();
        _resources = null;
    }

    protected override void RenderFrame(PixelSize pixelSize)
    {
        if (_resources == null)
            return;
        using (_resources.Swapchain.BeginDraw(pixelSize, out var image))
            _resources.Content.Render(image);
    }
}