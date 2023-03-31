using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using Arch.Core;
using Arch.Core.Extensions;
using GameDotNet.Core.ECS;
using GameDotNet.Core.ECS.Components;
using GameDotNet.Core.Graphics.Vulkan;
using GameDotNet.Core.Physics.Components;
using Silk.NET.Windowing;

namespace GameDotNet.Core.Graphics;

public sealed class VulkanRenderSystem : SystemBase, IDisposable
{
    private static readonly QueryDescription MeshQueryDesc = new QueryDescription().WithAll<RenderMesh, Translation>();
    private static readonly QueryDescription CameraQueryDesc = new QueryDescription().WithAll<Camera>();

    private readonly VulkanRenderer _renderer;
    private readonly Thread _renderThread;
    private readonly IView _view;
    private readonly Stopwatch _drawWatch;

    public VulkanRenderSystem(IView view) : base(MeshQueryDesc)
    {
        _view = view;
        _drawWatch = new();
        _renderer = new(_view);
        _renderThread = new(RenderLoop)
        {
            Name = "GameDotNet Render"
        };
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

        var e = ParentWorld.Create(new Tag("Triangle"),
                                   new Translation(Vector3.One),
                                   new RenderMesh(m));


        _renderer.UploadMesh(ref e.Get<RenderMesh>());

        return true;
    }

    public override void Update(TimeSpan delta)
    {
        if (!_renderThread.IsAlive)
            _renderThread.Start();
    }

    private void RenderLoop()
    {
        while (!_view.IsClosing)
        {
            var cam = ParentWorld.GetFirstEntity(CameraQueryDesc);

            _renderer.Draw(_drawWatch.Elapsed, ParentWorld.Query(Description).GetChunkIterator(), cam);

            _drawWatch.Restart();
        }
    }

    public void Dispose()
    {
        _renderThread.Join();

        _renderer.Dispose();
    }
}