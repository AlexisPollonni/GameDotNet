using System.Diagnostics;
using System.Numerics;
using Arch.Core;
using Arch.Core.Extensions;
using GameDotNet.Core.Physics.Components;
using GameDotNet.Management;
using GameDotNet.Management.ECS;
using GameDotNet.Management.ECS.Components;
using MessagePipe;
using Microsoft.Extensions.Logging;
using Serilog;
using Silk.NET.Maths;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using ThreadState = System.Threading.ThreadState;

namespace GameDotNet.Graphics.WGPU;

public sealed class WebGpuRenderSystem : SystemBase, IDisposable
{
    public TimingsRingBuffer RenderTimings { get; }
    
    
    private static readonly QueryDescription CameraQueryDesc = new QueryDescription().WithAny<Camera>();
    private static readonly QueryDescription MeshQueryDesc = new QueryDescription().WithAny<Mesh>();

    private readonly ILogger _logger;
    private readonly Thread _renderThread;
    private readonly Stopwatch _drawWatch;
    private readonly WebGpuContext _gpuContext;
    private readonly WebGpuRenderer _renderer;
    private readonly NativeViewManager _viewManager;

    private bool _isRenderPaused;
    private Vector2D<int> _lastFramebufferSize;

    public WebGpuRenderSystem(ILogger<WebGpuRenderSystem> logger, WebGpuContext gpuContext, WebGpuRenderer renderer, NativeViewManager viewManager) : base(CameraQueryDesc)
    {
        _logger = logger;
        _gpuContext = gpuContext;
        _renderer = renderer;
        _viewManager = viewManager;
        _drawWatch = new();
        _isRenderPaused = false;
        RenderTimings = new(512);
        _lastFramebufferSize = new(-1, -1);
      
        _renderThread = new(RenderLoop)
        {
            Name = "GameDotNet Render"
        };
    }

    public override async ValueTask<bool> Initialize(CancellationToken token = default)
    {
        if (_viewManager.MainView is null)
            return false;

        _viewManager.MainView.Resized.Subscribe(OnFramebufferResize);
        
        if (!await _gpuContext.Initialize(_viewManager.MainView, token))
            return false;
        
        await _renderer.Initialize(token);
        
        _gpuContext.ResizeSurface(new(_viewManager.MainView.Size.X, _viewManager.MainView.Size.Y));

        //TODO: Move this to asset manager when its implemented
        ParentWorld.Query<Mesh>(MeshQueryDesc, (in Entity e, ref Mesh mesh) =>
        {
            if (mesh.Vertices.Count is 0)
            {
                Log.Warning("Skipped mesh {Name} with no vertices", e.Get<Tag>().Name);
                return;
            }

            //model transform
            var model = Transform.FromEntity(e)?.ToMatrix() ?? Matrix4x4.Identity;
            
            _renderer.MeshInstances.Add(new(model, mesh));

            _renderer.UploadMeshes(mesh);
        });
        
        _renderer.WriteModelUniforms();

        return true;
    }
    
    public override void Update(TimeSpan delta)
    {
        if (_renderThread.ThreadState is ThreadState.Unstarted)
            _renderThread.Start();
    }

    public void Dispose()
    {
        _renderThread.Interrupt();
        if (_renderThread.IsAlive)
            _renderThread.Join();
        
        _gpuContext.Dispose();
    }

    private void RenderLoop()
    {
        var surfaceSize = _viewManager.MainView!.Size;
        while (!_viewManager.MainView.IsClosing)
        {
            _gpuContext?.Instance.ProcessEvents();
            var cam = ParentWorld.GetFirstEntity(CameraQueryDesc);
            _renderer.CurrentCamera = Transform.FromEntity(cam) ?? new();

            if (surfaceSize != _viewManager.MainView.Size)
            {
                _gpuContext?.ResizeSurface(new(_viewManager.MainView.Size.X, _viewManager.MainView.Size.Y));
                surfaceSize = _viewManager.MainView.Size;
            }
            
            {
                using var swTextureView = _gpuContext?.SwapChain?.GetCurrentTextureView();
                if(swTextureView is null) 
                    continue;
                _renderer.Draw(_drawWatch.Elapsed, swTextureView);
                
                _gpuContext?.SwapChain?.Present();
            }
            RenderTimings.Add(_drawWatch.Elapsed);
            _drawWatch.Restart();

            if (!Volatile.Read(ref _isRenderPaused)) continue;

            _logger.LogInformation("<Render> Render thread {Id} entering sleep", Environment.CurrentManagedThreadId);
            try
            {
                Thread.Sleep(Timeout.Infinite);
            }
            catch (ThreadInterruptedException)
            {
                _logger.LogInformation("<Render> Render thread {Id} resumed", Environment.CurrentManagedThreadId);
            }
        }
    }

    // Checks if view is minimized and if it is pause render thread
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