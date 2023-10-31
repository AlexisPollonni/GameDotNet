using System.Diagnostics;
using Arch.Core;
using GameDotNet.Management;
using GameDotNet.Management.ECS;
using Serilog;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace GameDotNet.Graphics.WGPU;

public sealed class WebGpuRenderSystem : SystemBase, IDisposable
{
    private static readonly QueryDescription CameraQueryDesc = new QueryDescription().WithAll<Camera>();

    private readonly ILogger _logger;
    private readonly Thread _renderThread;
    private readonly IView _view;
    private readonly Stopwatch _drawWatch;

    private bool _isRenderPaused;
    private Vector2D<int> _lastFramebufferSize;
    private WebGpuContext? _gpuContext;
    private WebGpuRenderer? _renderer;

    public WebGpuRenderSystem(ILogger logger, IView view) : base(CameraQueryDesc)
    {
        _logger = logger;
        _view = view;
        _drawWatch = new();
        _isRenderPaused = false;
        _lastFramebufferSize = new(-1, -1);
      
        _renderThread = new(RenderLoop)
        {
            Name = "GameDotNet Render"
        };
        _view.FramebufferResize += OnFramebufferResize;
    }

    public override async ValueTask<bool> Initialize()
    {
        var comp = new ShaderCompiler(_logger);

        var vert = await comp.TranslateGlsl("Assets/Mesh.vert", "Assets/");
        var frag = await comp.TranslateGlsl("Assets/Mesh.frag", "Assets/");

        _gpuContext = await WebGpuContext.Create(_logger, _view);
        _renderer = new(_gpuContext, _logger);
        
        await _renderer.Initialize(vert, frag);
        
        _gpuContext.ResizeSurface(new(_view.Size.X, _view.Size.Y));

        //TODO: Move this to asset manager when its implemented
        // ParentWorld.Query<Mesh>(MeshQueryDesc, (in Entity e, ref Mesh mesh) =>
        // {
        //     if (mesh.Vertices.Count is 0)
        //     {
        //         Log.Warning("Skipped mesh {Name} with no vertices", e.Get<Tag>().Name);
        //         return;
        //     }
        //
        //     var render = new RenderMesh(mesh);
        //     _renderer.UploadMesh(ref render);
        //     e.Add(render);
        // });

        return true;
    }
    
    public override void Update(TimeSpan delta)
    {
        if (!_renderThread.IsAlive)
            _renderThread.Start();
    }

    public void Dispose()
    {
        _renderThread.Interrupt();
        if (_renderThread.IsAlive)
            _renderThread.Join();
        
        _gpuContext?.Dispose();
    }

    private void RenderLoop()
    {
        var surfaceSize = _view.FramebufferSize;
        while (!_view.IsClosing)
        {
            _gpuContext?.Instance.ProcessEvents();
            var cam = ParentWorld.GetFirstEntity(CameraQueryDesc);

            if (surfaceSize != _view.FramebufferSize)
            {
                _gpuContext?.ResizeSurface(new(_view.FramebufferSize.X, _view.FramebufferSize.Y));
                surfaceSize = _view.Size;
            }

            var surfView = _gpuContext?.SwapChain.GetCurrentTextureView();
            if (surfView is null)
                continue;
            using (surfView)
            {
                _renderer?.Draw(_drawWatch.Elapsed, surfView);
                
                _gpuContext?.SwapChain.Present();
            }
            _drawWatch.Restart();

            if (!Volatile.Read(ref _isRenderPaused)) continue;

            Log.Information("<Render> Render thread {Id} entering sleep", Environment.CurrentManagedThreadId);
            try
            {
                Thread.Sleep(Timeout.Infinite);
            }
            catch (ThreadInterruptedException)
            {
                Log.Information("<Render> Render thread {Id} resumed", Environment.CurrentManagedThreadId);
            }
        }
    }

    private void OnFramebufferResize(Vector2D<int> size)
    {
        var last = _lastFramebufferSize;
        if (size.X is 0 || size.Y is 0)
        {
            if (last.X is not 0 || last.Y is not 0)
                Volatile.Write(ref _isRenderPaused, true);
        }
        else if (last.X is 0 || last.Y is 0)
        {
            Volatile.Write(ref _isRenderPaused, false);
            _renderThread.Interrupt();
        }

        _lastFramebufferSize = size;
    }
}