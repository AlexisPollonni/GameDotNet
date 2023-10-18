using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using Arch.Core;
using Arch.Core.Extensions;
using GameDotNet.Core.Physics.Components;
using GameDotNet.Graphics.Vulkan.Wrappers;
using GameDotNet.Management;
using GameDotNet.Management.ECS;
using GameDotNet.Management.ECS.Components;
using Serilog;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace GameDotNet.Graphics.Vulkan;

public sealed class VulkanRenderSystem : SystemBase, IDisposable
{
    private static readonly QueryDescription RenderQueryDesc = new QueryDescription().WithAll<RenderMesh>();

    private static readonly QueryDescription MeshQueryDesc =
        new QueryDescription().WithAll<Mesh>().WithNone<RenderMesh>();

    private static readonly QueryDescription CameraQueryDesc = new QueryDescription().WithAll<Camera>();

    private DefaultVulkanContext? _context;
    private VulkanRenderer? _renderer;

    private readonly ILogger _logger;
    private readonly Thread _renderThread;
    private readonly IView _view;
    private readonly Stopwatch _drawWatch;

    private bool _isRenderPaused;
    private Vector2D<int> _lastFramebufferSize;

    public VulkanRenderSystem(ILogger logger, IView view) : base(RenderQueryDesc)
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
        _context = DefaultVulkanContext.TryCreateForView(_view).context;
        _renderer = new(_context!);

        var comp = new ShaderCompiler(_logger);

        var vert = await comp.TranslateGlsl("Assets/Mesh.vert", "Assets/");
        var frag = await comp.TranslateGlsl("Assets/Mesh.frag", "Assets/");
        
        _renderer.Initialize(vert, frag);
        
        Mesh m = new(new()
        {
            new(new(1, 1, 0), new(), Color.Blue),
            new(new(-1, 1, 0), new(), Color.Red),
            new(new(0, -1, 0), new(), Color.Green),
        });

        ParentWorld.Create(new Tag("Triangle"),
                           new Translation(Vector3.Zero),
                           m);


        //TODO: Move this to asset manager when its implemented
        ParentWorld.Query<Mesh>(MeshQueryDesc, (in Entity e, ref Mesh mesh) =>
        {
            if (mesh.Vertices.Count is 0)
            {
                Log.Warning("Skipped mesh {Name} with no vertices", e.Get<Tag>().Name);
                return;
            }

            var render = new RenderMesh(mesh);
            _renderer.UploadMesh(ref render);
            e.Add(render);
        });

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
        _renderThread.Join();

        _renderer?.Dispose();
        _context?.Dispose();
    }

    private void RenderLoop()
    {
        while (!_view.IsClosing)
        {
            var cam = ParentWorld.GetFirstEntity(CameraQueryDesc);

            _renderer?.Draw(_drawWatch.Elapsed, ParentWorld.Query(Description).GetChunkIterator(), cam);

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