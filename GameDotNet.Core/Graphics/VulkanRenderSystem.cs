using System.Buffers;
using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using GameDotNet.Core.ECS;
using GameDotNet.Core.Graphics.Vulkan;
using GameDotNet.Core.Physics.Components;
using Silk.NET.Windowing;

namespace GameDotNet.Core.Graphics;

public sealed class VulkanRenderSystem : GenericSystem<Translation, RenderMesh>, IDisposable
{
    private readonly VulkanRenderer _renderer;
    private readonly Thread _renderThread;
    private readonly IView _view;
    private readonly Stopwatch _drawWatch;

    public VulkanRenderSystem(World world, IView view) : base(world)
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

        var e = World.EntityManager.CreateEntity("Triangle")
                     .Add<Translation>(new(Vector3.One))
                     .Add<RenderMesh>(new(m));

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
            var entities = World.EntityManager.Get(GetBoundEntities());

            var m = ArrayPool<Mesh>.Shared.Rent(entities.Length);
            for (var i = 0; i < entities.Length; i++) 
                m[i] = entities[i].Get<RenderMesh>().Mesh;

            _renderer.Draw(_drawWatch.Elapsed, m.AsSpan(..entities.Length));
            ArrayPool<Mesh>.Shared.Return(m);

            _drawWatch.Restart();
        }
    }

    public void Dispose()
    {
        _renderThread.Join();

        _renderer.Dispose();
    }
}