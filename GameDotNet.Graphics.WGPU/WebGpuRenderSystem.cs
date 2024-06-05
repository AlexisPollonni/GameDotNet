using System.Drawing;
using System.Numerics;
using System.Reactive.Linq;
using Arch.Core;
using Arch.Core.Extensions;
using GameDotNet.Core.Physics.Components;
using GameDotNet.Core.Tools.Containers;
using GameDotNet.Core.Tools.Extensions;
using GameDotNet.Graphics.Abstractions;
using GameDotNet.Management;
using GameDotNet.Management.ECS;
using GameDotNet.Management.ECS.Components;
using MessagePipe;
using Microsoft.Extensions.Logging;
using Serilog;
using Silk.NET.WebGPU;

namespace GameDotNet.Graphics.WGPU;

public sealed class WebGpuRenderSystem : SystemBase, IDisposable
{
    public TimingsRingBuffer RenderTimings { get; }

    private static readonly QueryDescription CameraQueryDesc = new QueryDescription().WithAny<Camera>();
    private static readonly QueryDescription MeshQueryDesc = new QueryDescription().WithAny<Mesh>();

    private readonly ILogger<WebGpuRenderSystem> _logger;
    private readonly SceneManager _sceneManager;
    private readonly DisposableList _disposables;
    private readonly WebGpuContext _gpuContext;
    private readonly WebGpuRenderer _renderer;
    private readonly NativeViewManager _viewManager;

    private Size _lastFramebufferSize;

    public WebGpuRenderSystem(ILogger<WebGpuRenderSystem> logger,
                              SceneManager sceneManager,
                              WebGpuContext gpuContext,
                              WebGpuRenderer renderer,
                              NativeViewManager viewManager) : base(new(0, true, false))
    {
        _logger = logger;
        _sceneManager = sceneManager;
        _disposables = new();
        _gpuContext = gpuContext;
        _renderer = renderer;
        _viewManager = viewManager;
        RenderTimings = new(512);
        _lastFramebufferSize = new(-1, -1);

        var viewChanged = viewManager.MainViewChanged.AsObservable().Where(v => v is not null).Select(v => v!);

        viewChanged.FirstAsync()
                   .SelectMany(async (v, token) => await InitializeGraphicsResources(v, token))
                   .Subscribe()
                   .DisposeWith(_disposables);

        viewChanged.SelectMany(v => v.Resized.AsObservable()).Subscribe(OnFramebufferResize).DisposeWith(_disposables);
    }

    private async ValueTask<bool> InitializeGraphicsResources(INativeView view, CancellationToken token = default)
    {
        await _gpuContext.Initialize(view, token);
        await _renderer.Initialize(token);

        _gpuContext.ResizeSurface(view.Size);

        //TODO: Move this to asset manager when its implemented
        _sceneManager.World.Query(MeshQueryDesc,
                             (Entity e, ref Mesh mesh) =>
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

        IsRunning = true;
        return true;
    }

    public override void Update(TimeSpan delta)
    {
        if (_viewManager.MainView is null) return;
        if (_viewManager.MainView.IsClosing)
        {
            IsRunning = false;
            return;
        }

        if (!_gpuContext.IsInitialized) return;
        
        var surfaceSize = _gpuContext.Surface.Size;

        _gpuContext.Device.WGpuDevicePoll();

        if (surfaceSize != _viewManager.MainView.Size)
        {
            _gpuContext.ResizeSurface(_viewManager.MainView.Size);
            surfaceSize = _viewManager.MainView.Size;
        }
        if(surfaceSize is {IsEmpty:true}) return;

        {
            var surfaceTexture = _gpuContext.Surface?.GetCurrentTexture();
            if (surfaceTexture?.Status is not SurfaceGetCurrentTextureStatus.Success) return;

            using var swTextureView = surfaceTexture.Value.Texture?.CreateTextureView();

            var cam = _sceneManager.World.GetFirstEntity(CameraQueryDesc);
            var camTransform = Transform.FromEntity(cam) ?? new();
            ref readonly var camData = ref cam.Get<Camera>();

            _renderer.WriteCameraUniform(in surfaceSize, in camTransform, in camData);

            _renderer.Draw(delta, swTextureView);

            _gpuContext.Surface!.Present();
        }
        RenderTimings.Add(delta);
    }

    public void Dispose()
    {
        _gpuContext.Dispose();
        _disposables.Dispose();
    }

    // Checks if view is minimized and if it is pause render job
    private void OnFramebufferResize(Size size)
    {
        var last = _lastFramebufferSize;
        if (size.Width is 0 || size.Height is 0)
        {
            if (last.Width is not 0 || last.Height is not 0)
            {
                IsRunning = false;
                _logger.LogInformation("<Render> Render thread {Id} entering sleep", Environment.CurrentManagedThreadId);
            }
        }
        else if (last.Width is 0 || last.Height is 0)
        {
            IsRunning = true;
            _logger.LogInformation("<Render> Render thread {Id} resumed", Environment.CurrentManagedThreadId);
        }

        _lastFramebufferSize = size;
    }
}