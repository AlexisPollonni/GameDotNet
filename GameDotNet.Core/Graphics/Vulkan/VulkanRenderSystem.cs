using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using Arch.Core;
using Arch.Core.Extensions;
using GameDotNet.Core.ECS;
using GameDotNet.Core.ECS.Components;
using GameDotNet.Core.Physics.Components;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace GameDotNet.Core.Graphics.Vulkan;

public sealed class VulkanRenderSystem : SystemBase, IDisposable
{
    private static readonly QueryDescription RenderQueryDesc = new QueryDescription().WithAll<RenderMesh>();

    private static readonly QueryDescription MeshQueryDesc =
        new QueryDescription().WithAll<Mesh>().WithNone<RenderMesh>();

    private static readonly QueryDescription CameraQueryDesc = new QueryDescription().WithAll<Camera>();

    private readonly VulkanRenderer _renderer;
    private readonly Thread _renderThread;
    private readonly IView _view;
    private readonly Stopwatch _drawWatch;
    private readonly SemaphoreSlim _renderSem;

    private Vector2D<int> _lastFramebufferSize;

    public VulkanRenderSystem(IView view) : base(RenderQueryDesc)
    {
        _view = view;
        _drawWatch = new();
        _renderer = new(_view);
        _lastFramebufferSize = new(-1, -1);
        _renderSem = new(1, 1);
        _renderThread = new(RenderLoop)
        {
            Name = "GameDotNet Render"
        };
        _view.FramebufferResize += OnFramebufferResize;
    }

    public override bool Initialize()
    {
        _renderer.Initialize();
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
        ParentWorld.Query(MeshQueryDesc, (in Entity e, ref Mesh mesh) =>
        {
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
        _renderSem.Dispose();
        _renderThread.Join();

        _renderer.Dispose();
    }

    private void RenderLoop()
    {
        while (!_view.IsClosing)
        {
            _renderSem.Wait();
            try
            {
                var cam = ParentWorld.GetFirstEntity(CameraQueryDesc);

                _renderer.Draw(_drawWatch.Elapsed, ParentWorld.Query(Description).GetChunkIterator(), cam);
            }
            finally
            {
                _renderSem.Release();
            }

            _drawWatch.Restart();
        }
    }

    private void OnFramebufferResize(Vector2D<int> size)
    {
        var last = _lastFramebufferSize;
        if (size.X is 0 || size.Y is 0)
        {
            if (last.X is not 0 || last.Y is not 0)
                _renderSem.WaitAsync();
        }
        else if (last.X is 0 || last.Y is 0)
        {
            _renderSem.Release();
        }

        _lastFramebufferSize = size;
    }
}