using System.Drawing;
using GameDotNet.Graphics.WGPU.Wrappers;
using Silk.NET.WebGPU;
using Color = Silk.NET.WebGPU.Color;
using RenderPassColorAttachment = GameDotNet.Graphics.WGPU.Wrappers.RenderPassColorAttachment;
using Texture = GameDotNet.Graphics.WGPU.Wrappers.Texture;

namespace GameDotNet.Graphics.WGPU;

public class WebGpuRenderer
{
    private readonly WebGpuContext _context;

    public WebGpuRenderer(WebGpuContext context)
    {
        _context = context;
    }


    public void Draw(TimeSpan delta, Texture texture)
    {
        var view = texture.CreateTextureView();

        var encoder = _context.Device.CreateCommandEncoder("render-command-encoder");

        var colorAttach = new RenderPassColorAttachment
        {
            View = view,
            LoadOp = LoadOp.Clear,
            StoreOp = StoreOp.Store,
            ClearValue = new(255)
        };
        var renderPass = encoder.BeginRenderPass("render-encoder-begin", new[] { colorAttach });
        
        
        
        renderPass.End();
        var buffer = encoder.Finish("render-encoder-finish");
        _context.Device.Queue.Submit(buffer);
    }
}