using Microsoft.Extensions.Logging;
using Silk.NET.WebGPU;
using ColorTargetState = GameDotNet.Graphics.WGPU.Wrappers.ColorTargetState;
using FragmentState = GameDotNet.Graphics.WGPU.Wrappers.FragmentState;
using RenderPassColorAttachment = GameDotNet.Graphics.WGPU.Wrappers.RenderPassColorAttachment;
using RenderPipeline = GameDotNet.Graphics.WGPU.Wrappers.RenderPipeline;
using TextureView = GameDotNet.Graphics.WGPU.Wrappers.TextureView;
using VertexState = GameDotNet.Graphics.WGPU.Wrappers.VertexState;

namespace GameDotNet.Graphics.WGPU;

public class WebGpuRenderer
{
    private readonly WebGpuContext _context;
    private readonly ILogger _logger;
    private RenderPipeline _meshPipeline;

    public WebGpuRenderer(WebGpuContext context, ILogger logger)
    {
        _context = context;
        _logger = logger;
    }

    public async ValueTask Initialize(SpirVShader vertSrc, SpirVShader fragSrc, CancellationToken token = default)
    {
        var vertShader = new WebGpuShader(_context, vertSrc, _logger);
        var fragShader = new WebGpuShader(_context, fragSrc, _logger);

        await vertShader.Compile(token);
        await fragShader.Compile(token);

        _meshPipeline = CreateRenderPipeline(vertShader, fragShader, token);
    }


    public void Draw(TimeSpan delta, TextureView? view)
    {
        var encoder = _context.Device.CreateCommandEncoder("render-command-encoder");

        var colorAttach = new RenderPassColorAttachment
        {
            View = view,
            LoadOp = LoadOp.Clear,
            StoreOp = StoreOp.Store,
            ClearValue = new(255)
        };
        var renderPass = encoder.BeginRenderPass("render-encoder-begin", new[] { colorAttach });


        //renderPass.SetPipeline(_meshPipeline);


        renderPass.End();
        var buffer = encoder.Finish("render-encoder-finish");
        _context.Device.Queue.Submit(buffer);
    }

    private RenderPipeline CreateRenderPipeline(WebGpuShader vert, WebGpuShader frag, CancellationToken token = default)
    {
        const TextureFormat surfaceFormat = TextureFormat.Bgra8Unorm;
        
        var blendState = new BlendState
        {
            Color = new()
            {
                SrcFactor = BlendFactor.SrcAlpha,
                DstFactor = BlendFactor.OneMinusSrcAlpha,
                Operation = BlendOperation.Add
            },
            Alpha = new()
            {
                SrcFactor = BlendFactor.Zero,
                DstFactor = BlendFactor.One,
                Operation = BlendOperation.Add
            }
        };
        var vertexState = new VertexState
        {
            Module = vert.Module!,
            EntryPoint = "main",
            
        };
        var fragState = new FragmentState
        {
            Module = frag.Module!,
            EntryPoint = "main",
            ColorTargets = new[]
            {
                new ColorTargetState
                {
                    BlendState = blendState,
                    Format = surfaceFormat,
                    WriteMask = ColorWriteMask.All
                }
            }
        };

        var primState = new PrimitiveState
        {
            Topology = PrimitiveTopology.TriangleList,
            CullMode = CullMode.None,
            FrontFace = FrontFace.Ccw,
            StripIndexFormat = IndexFormat.Uint32
        };
        var multisampleState = new MultisampleState
        {
            //1 sample per pixel
            Count = 1,
            Mask = ~0u,
            AlphaToCoverageEnabled = false
        };

        var vertLayout = vert.GetPipelineGroupBindLayouts();
        var fragLayout = frag.GetPipelineGroupBindLayouts();
        
        var layout = _context.Device.CreatePipelineLayout("render-pipeline-layout", vertLayout.Concat(fragLayout).ToArray());
        
        return _context.Device.CreateRenderPipeline("render-pipeline", layout, vertexState, primState, multisampleState,
                                             fragmentState: fragState);
    }
}