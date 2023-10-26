using System.Diagnostics;
using System.Numerics;
using System.Threading.Tasks.Sources;
using Arch.Core;
using Arch.Core.Extensions;
using GameDotNet.Core.Physics.Components;
using GameDotNet.Management;
using GameDotNet.Management.ECS;
using GameDotNet.Management.ECS.Components;
using Microsoft.Extensions.Logging;
using Serilog;
using Silk.NET.Core;
using Silk.NET.Core.Contexts;
using Silk.NET.Maths;
using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Extensions.WGPU;
using Silk.NET.Windowing;
using Adapter = GameDotNet.Graphics.WGPU.Wrappers.Adapter;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using Instance = GameDotNet.Graphics.WGPU.Wrappers.Instance;
using Surface = GameDotNet.Graphics.WGPU.Wrappers.Surface;

namespace GameDotNet.Graphics.WGPU;

public sealed class WebGpuRenderer : SystemBase, IDisposable
{
    private static readonly QueryDescription CameraQueryDesc = new QueryDescription().WithAll<Camera>();

    private readonly ILogger _logger;
    private readonly Thread _renderThread;
    private readonly IView _view;
    private readonly Stopwatch _drawWatch;

    private bool _isRenderPaused;
    private Vector2D<int> _lastFramebufferSize;

    public WebGpuRenderer(ILogger logger, IView view) : base(CameraQueryDesc)
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


        var api = WebGPU.GetApi();
        var instance = new Instance(api);

        var surface = CreateSurfaceFromView(instance, _view);
        
        var adapter = await instance.RequestAdapterAsync(surface, PowerPreference.HighPerformance, false,BackendType.Vulkan);

        adapter.GetProperties(out var properties);

        adapter.GetLimits(out var limits);

        var device = await adapter.RequestDeviceAsync(limits: limits.Limits, label: "Device", nativeFeatures: new[]
        {
            NativeFeature.PushConstants, NativeFeature.MultiDrawIndirect
        });
        
        device.SetUncapturedErrorCallback((type, message) =>
        {
            _logger.LogError("[WebGPU][{ErrorType}: {Message}]", type, message);
        });

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

    private static unsafe Surface CreateSurfaceFromView(Instance instance, IView view)
    {
        var nat = view.Native ??
                  throw new PlatformNotSupportedException("No native view found to initialize WebGPU from");

        Surface surface;
        if (nat.Kind.HasFlag(NativeWindowFlags.Win32))
        {
            surface = instance.CreateSurfaceFromWindowsHWND((void*)nat.Win32!.Value.HInstance,
                                                            (void*)nat.Win32.Value.Hwnd,
                                                            "surface-create-Win32");
        }
        else if (nat.Kind.HasFlag(NativeWindowFlags.X11))
        {
            surface = instance.CreateSurfaceFromXlibWindow((void*)nat.X11!.Value.Display, (uint)nat.X11.Value.Window,
                                                           "surface-create-X11");
        }
        else if (nat.Kind.HasFlag(NativeWindowFlags.Cocoa))
        {
            surface = instance.CreateSurfaceFromMetalLayer((void*)nat.Cocoa, "surface-create-MetalCocoa");
        }
        else throw new PlatformNotSupportedException("Native window type is not supported by webpgu");

        return surface;
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
    }

    private void RenderLoop()
    {
        while (!_view.IsClosing)
        {
            var cam = ParentWorld.GetFirstEntity(CameraQueryDesc);

            //_renderer?.Draw(_drawWatch.Elapsed, ParentWorld.Query(Description).GetChunkIterator(), cam);

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