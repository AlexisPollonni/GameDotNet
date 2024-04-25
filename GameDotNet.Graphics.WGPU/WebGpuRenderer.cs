using System.Numerics;
using System.Runtime.CompilerServices;
using GameDotNet.Core.Physics.Components;
using GameDotNet.Core.Tools.Extensions;
using GameDotNet.Graphics.WGPU.Extensions;
using Microsoft.Extensions.Logging;
using Silk.NET.Maths;
using Silk.NET.WebGPU;
using BindGroup = GameDotNet.Graphics.WGPU.Wrappers.BindGroup;
using BindGroupEntry = GameDotNet.Graphics.WGPU.Wrappers.BindGroupEntry;
using Buffer = GameDotNet.Graphics.WGPU.Wrappers.Buffer;
using ColorTargetState = GameDotNet.Graphics.WGPU.Wrappers.ColorTargetState;
using FragmentState = GameDotNet.Graphics.WGPU.Wrappers.FragmentState;
using RenderPassColorAttachment = GameDotNet.Graphics.WGPU.Wrappers.RenderPassColorAttachment;
using RenderPassDepthStencilAttachment = GameDotNet.Graphics.WGPU.Wrappers.RenderPassDepthStencilAttachment;
using RenderPipeline = GameDotNet.Graphics.WGPU.Wrappers.RenderPipeline;
using Texture = GameDotNet.Graphics.WGPU.Wrappers.Texture;
using TextureView = GameDotNet.Graphics.WGPU.Wrappers.TextureView;

namespace GameDotNet.Graphics.WGPU;

public class WebGpuRenderer
{
    public Transform CurrentCamera { get; set; }
    public IList<MeshInstanceRender> MeshInstances => _meshInstances;
    
    
    private readonly WebGpuContext _context;
    private readonly ShaderCompiler _compiler;
    private readonly ILogger<WebGpuRenderer> _logger;
    private RenderPipeline _meshPipeline;

    private readonly Dictionary<Mesh, MeshRenderInfo> _meshBufferCache;
    private readonly List<MeshInstanceRender> _meshInstances;
    private BindGroup _uniformBind;
    private Buffer _modelUniformBuffer;
    private Buffer _cameraUniformBuffer;
    private Texture? _depthTexture;
    private TextureView? _depthTextureView;

    public WebGpuRenderer(WebGpuContext context, ShaderCompiler compiler, ILogger<WebGpuRenderer> logger)
    {
        _context = context;
        _compiler = compiler;
        _logger = logger;

        _meshBufferCache = new();
        _meshInstances = new();
    }

    public async ValueTask Initialize(CancellationToken token = default)
    {
        if (!_context.IsInitialized) throw new InvalidOperationException("Context not initialized");
        
        var vert = await _compiler.TranslateGlsl("Assets/Mesh.vert", "Assets/", token);
        var frag = await _compiler.TranslateGlsl("Assets/Mesh.frag", "Assets/", token);
        
        var vertShader = new WebGpuShader(_context, vert, _logger);
        var fragShader = new WebGpuShader(_context, frag, _logger);

        await vertShader.Compile(token);
        await fragShader.Compile(token);

        _meshPipeline = await CreateRenderPipeline(vertShader, fragShader, token);

        _cameraUniformBuffer = _context.Device.CreateBuffer("uniform-buffer-camera", false,
                                                            (ulong)Unsafe.SizeOf<CameraRenderInfo>(),
                                                            BufferUsage.Uniform | BufferUsage.CopyDst);
        _modelUniformBuffer = _context.Device.CreateBuffer("uniform-buffer-models", false,
                                                           (ulong)Unsafe.SizeOf<Matrix4x4>() * 128,
                                                           BufferUsage.Uniform | BufferUsage.CopyDst);

        using var camLayout = _meshPipeline.GetBindGroupLayout(0);

        var bindGroupEntries = new BindGroupEntry[]
        {
            new()
            {
                Binding = 0,
                Buffer = _cameraUniformBuffer,
                Offset = 0,
                Size = (ulong)Unsafe.SizeOf<CameraRenderInfo>()
            },
            new()
            {
                Binding = 1,
                Buffer = _modelUniformBuffer,
                Offset = 0,
                Size = (ulong)Unsafe.SizeOf<Matrix4x4>()
            }
        };

        _uniformBind = _context.Device.CreateBindGroup("uniform-bindgroup", camLayout, bindGroupEntries);
    }

    public void WriteModelUniforms()
    {
        var modelMatrices = new Matrix4x4[_meshInstances.Count];
        for (var i = 0; i < _meshInstances.Count; i++)
        {
            modelMatrices[i] = Matrix4x4.Transpose(_meshInstances[i].Model);
        }
        
        _context.Device!.Queue.WriteBuffer<Matrix4x4>(_modelUniformBuffer, modelMatrices);
    }

    public void WriteCameraUniform(Extent3D viewSize)
    {
        // camera position
        var view = Transform.ToMatrix(Vector3.One, CurrentCamera.Rotation, CurrentCamera.Translation);
        Matrix4x4.Invert(view, out view);

        view = Matrix4x4.Transpose(view);
        
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(Scalar.DegreesToRadians(70f),
                                                                (float)viewSize.Width / viewSize.Height,
                                                                0.1f, 5000f);

        projection = Matrix4x4.Transpose(projection);
        var camData = new CameraRenderInfo(view, projection);
        
        _context.Device!.Queue.WriteBuffer<CameraRenderInfo>(_cameraUniformBuffer, camData.AsSpan());
    }

    public void RecreateDepthTexture(Extent3D size)
    {
        _depthTextureView?.Dispose();
        _depthTexture?.Dispose();
        
        _depthTexture = _context.Device!.CreateTexture("texture-depth", 
                                                      TextureUsage.RenderAttachment, TextureDimension.Dimension2D,
                                      size, 
                                                      TextureFormat.Depth24Plus, 1, 1,
                                                      new [] { TextureFormat.Depth24Plus });

        _depthTextureView = _depthTexture.CreateTextureView("texture-view-depth", TextureFormat.Depth24Plus,
                                        TextureViewDimension.Dimension2D,
                                        0, 1, 0, 1, TextureAspect.DepthOnly);
    }

    public unsafe void UploadMeshes(params Mesh[] mesh)
    {
        using var cmd = _context.Device!.CreateCommandEncoder("mesh-upload");
        foreach (var m in mesh)
        {
            if(_meshBufferCache.ContainsKey(m))
                continue;
            
            var vBuffer = _context.Device.CreateBuffer("vertex-buffer", false, 
                                                       (ulong)(m.Vertices.Count * sizeof(Vertex)),
                                                       BufferUsage.CopyDst | BufferUsage.Vertex);

            var iBuffer = _context.Device.CreateBuffer("index-buffer", false,
                                                       (ulong)(m.Indices.Count * sizeof(uint)),
                                                       BufferUsage.CopyDst | BufferUsage.Index);


            cmd.WriteBuffer<Vertex>(vBuffer, m.Vertices.ToArray());
            cmd.WriteBuffer<uint>(iBuffer, m.Indices.ToArray());
            
            _meshBufferCache.Add(m, new(vBuffer, iBuffer));
        }

        using var cmdBuff = cmd.Finish("mesh-upload-cmd");
        _context.Device.Queue.Submit(cmdBuff);
    }


    public void Draw(TimeSpan delta, TextureView? view)
    {
        if (view?.Texture is null) return;

        if (_depthTexture is null || view.Texture.Size.Width != _depthTexture.Size.Width || view.Texture.Size.Height != _depthTexture.Size.Height)
        {
            RecreateDepthTexture(view.Texture.Size);
        }
        
        WriteCameraUniform(view.Texture.Size);
        
        
        using var encoder = _context.Device!.CreateCommandEncoder("render-command-encoder");

        var colorAttach = new RenderPassColorAttachment
        {
            View = view,
            LoadOp = LoadOp.Clear,
            StoreOp = StoreOp.Store,
            ClearValue = new(255)
        };
        var depthAttach = new RenderPassDepthStencilAttachment
        {
            View = _depthTextureView,
            DepthClearValue = 1,
            DepthLoadOp = LoadOp.Clear,
            DepthStoreOp = StoreOp.Store,
            DepthReadOnly = false,
            StencilClearValue = 0,
            StencilLoadOp = LoadOp.Undefined, //Both undefined on Dawn implementation, clear and store on WGPU
            StencilStoreOp = StoreOp.Undefined,
            StencilReadOnly = true
        };
        using var renderPass = encoder.BeginRenderPass("render-encoder-begin", new[] { colorAttach }, depthAttach);
        
        renderPass.SetPipeline(_meshPipeline);
        
        // for (var i = 0; i < _meshInstances.Count; i++)
        // {
        //     var instance = _meshInstances[i];
        //     if (!_meshBufferCache.TryGetValue(instance.Mesh, out var meshData))
        //     {
        //         _logger.LogWarning("mesh was not loaded");
        //         continue;
        //     }
        //
        //     renderPass.SetVertexBuffer(0, meshData.VertexBuffer, 0, meshData.VertexBuffer.SizeInBytes);
        //     renderPass.SetIndexBuffer(meshData.IndexBuffer, IndexFormat.Uint32, 0, meshData.IndexBuffer.SizeInBytes);
        //     
        //     //renderPass.SetBindGroup(0, _uniformBind, new uint[] { 0, (uint)i });
        //
        //     renderPass.DrawIndexed((uint)instance.Mesh.Indices.Count, 1, 0, 0, 0);
        // }


        renderPass.End();
        using var buffer = encoder.Finish("render-encoder-finish");
        _context.Device.Queue.Submit(buffer);
    }

    private ValueTask<RenderPipeline> CreateRenderPipeline(WebGpuShader vert, WebGpuShader frag, CancellationToken token = default)
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
        var vertexState = vert.GetVertexState();
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
            StripIndexFormat = IndexFormat.Undefined
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

        //using webgpu autolayout for now so ignore this. Change when automatic resource alloc/render graph dev started
        var layout = _context.Device!.CreatePipelineLayout("render-pipeline-layout", vertLayout.Concat(fragLayout).ToArray());

        // Defaults face state for Dawn
        var stencilFaceState = new StencilFaceState
        {
            Compare = CompareFunction.Always,
            FailOp = StencilOperation.Keep,
            PassOp = StencilOperation.Keep,
            DepthFailOp = StencilOperation.Keep
        };
        var depthState = new DepthStencilState
        {
            DepthCompare = CompareFunction.Less,
            DepthWriteEnabled = true,
            Format = TextureFormat.Depth24Plus,
            StencilReadMask = 0,
            StencilWriteMask = 0,
            StencilFront = stencilFaceState,
            StencilBack = stencilFaceState
        };
        var pipeline = _context.Device.CreateRenderPipelineAsync("render-pipeline", vertexState, primState, multisampleState,
                                                         null, depthState, fragState, token).WaitWhilePollingAsync(_context, token);

        return pipeline;
    }
}

internal readonly struct MeshRenderInfo(Buffer vertexBuffer, Buffer indexBuffer)
{
    public Buffer VertexBuffer { get; } = vertexBuffer;
    public Buffer IndexBuffer { get; } = indexBuffer;
}

internal readonly struct CameraRenderInfo(Matrix4x4 view, Matrix4x4 projection)
{
    public Matrix4x4 View { get; } = view;
    public Matrix4x4 Projection { get; } = projection;
}

public readonly struct MeshInstanceRender(Matrix4x4 model, Mesh mesh)
{
    public Matrix4x4 Model { get; } = model;
    public Mesh Mesh { get; } = mesh;
}