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
using Query = GameDotNet.Core.ECS.Query;

namespace GameDotNet.Core.Graphics;

public sealed class VulkanRenderSystem : SystemBase, IDisposable
{
    private readonly VulkanRenderer _renderer;
    private readonly Thread _renderThread;
    private readonly IView _view;
    private readonly Stopwatch _drawWatch;

    public VulkanRenderSystem(IView view) : base(Query.All(typeof(RenderMesh), typeof(Translation)))
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
            new(new(1, 1, 0), new(2, 2, 2), Color.Blue),
            new(new(-1, 1, 0), new(3, 3, 3), Color.Red),
            new(new(0, 0, 0), new(4, 4, 4), Color.Green),

            new(new(0, 0, 0), new(2, 2, 2), Color.Blue),
            new(new(1, 0, 0), new(3, 3, 3), Color.Red),
            new(new(1, 1, 0), new(4, 4, 4), Color.Green)
        });

        var e = ParentWorld.Create(new Tag("Triangle"),
            new Translation(Vector3.One),
            new RenderMesh(m));


        _renderer.UploadMesh(ref e.Get<RenderMesh>().Mesh);

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
            _renderer.Draw(_drawWatch.Elapsed,  ParentWorld.Query(Description).GetChunkIterator());

            _drawWatch.Restart();
        }
    }

    public void Dispose()
    {
        _renderThread.Join();

        _renderer.Dispose();
    }
}