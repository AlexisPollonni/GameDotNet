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

namespace GameDotNet.Graphics.WGPU;

public sealed class WebGpuRenderSystem : SystemBase, IDisposable
{
    public TimingsRingBuffer RenderTimings { get; }


    private static readonly QueryDescription CameraQueryDesc = new QueryDescription().WithAny<Camera>();
    private static readonly QueryDescription MeshQueryDesc = new QueryDescription().WithAny<Mesh>();

    private readonly ILogger<WebGpuRenderSystem> _logger;
    private readonly WebGpuContext _gpuContext;
    private readonly WebGpuRenderer _renderer;
    private readonly NativeViewManager _viewManager;

    private Vector2D<int> _lastFramebufferSize;

    public WebGpuRenderSystem(ILogger<WebGpuRenderSystem> logger, Universe universe, WebGpuContext gpuContext,
                              WebGpuRenderer renderer, NativeViewManager viewManager)
        : base(universe, new(0, true))
    {
        _logger = logger;
        _gpuContext = gpuContext;
        _renderer = renderer;
        _viewManager = viewManager;
        RenderTimings = new(512);
        _lastFramebufferSize = new(-1, -1);
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
        Universe.World.Query(MeshQueryDesc, (Entity e, ref Mesh mesh) =>
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
        RenderLoop(delta);
    }

    public void Dispose()
    {
        _gpuContext.Dispose();
    }

    private void RenderLoop(TimeSpan delta)
    {
        if (_viewManager.MainView!.IsClosing)
        {
            IsRunning = false;
            return;
        }

        var surfaceSize = new Vector2D<int>(_gpuContext.SwapChain!.Size.Width, _gpuContext.SwapChain.Size.Height);

        _gpuContext.Instance.ProcessEvents();

        var cam = Universe.World.GetFirstEntity(CameraQueryDesc);
        _renderer.CurrentCamera = Transform.FromEntity(cam) ?? new();

        if (surfaceSize != _viewManager.MainView.Size)
        {
            _gpuContext.ResizeSurface(new(_viewManager.MainView.Size.X, _viewManager.MainView.Size.Y));
        }

        {
            using var swTextureView = _gpuContext.SwapChain?.GetCurrentTextureView();
            if (swTextureView is null)
                return;
            _renderer.Draw(delta, swTextureView);

            _gpuContext.SwapChain?.Present();
        }
        RenderTimings.Add(delta);
    }

    // Checks if view is minimized and if it is pause render job
    private void OnFramebufferResize(Vector2D<int> size)
    {
        var last = _lastFramebufferSize;
        if (size.X is 0 || size.Y is 0)
        {
            if (last.X is not 0 || last.Y is not 0)
            {
                IsRunning = false;
                _logger.LogInformation("<Render> Render thread {Id} entering sleep",
                                       Environment.CurrentManagedThreadId);
            }
        }
        else if (last.X is 0 || last.Y is 0)
        {
            IsRunning = true;
            _logger.LogInformation("<Render> Render thread {Id} resumed", Environment.CurrentManagedThreadId);
        }

        _lastFramebufferSize = size;
    }
}