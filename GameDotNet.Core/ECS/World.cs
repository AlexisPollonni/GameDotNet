using System.Diagnostics;

namespace GameDotNet.Core.ECS;

public sealed class World : IDisposable
{
    public EntityManager EntityManager { get; }
    public ComponentStoreBase ComponentStore { get; }

    private readonly List<SystemBase> _systems;
    private readonly Stopwatch _dtWatch;

    public World(ComponentStoreBase componentStore)
    {
        ComponentStore = componentStore;

        EntityManager = new(this);
        _systems = new();
        _dtWatch = new();
    }

    public void RegisterSystem<T>(T system) where T : SystemBase
    {
        _systems.Add(system);
    }

    public void Initialize()
    {
        if (_systems.Any(system => !system.Initialize()))
            throw new("Couldn't initialize system");

        _dtWatch.Start();
    }

    public void Update()
    {
        if (!_dtWatch.IsRunning)
            throw new("World update called but world not initialized");

        //TODO: Parallel updates and system priorities
        foreach (var system in _systems)
        {
            system.RefreshEntities(); //TODO: Refresh only when entities added
            system.Update(_dtWatch.Elapsed);
        }

        _dtWatch.Restart();
    }

    public void Dispose()
    {
        foreach (var system in _systems)
            if (system is IDisposable d)
                d.Dispose();
    }
}