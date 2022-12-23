using Arch.Core;
using Collections.Pooled;
using Serilog;

namespace GameDotNet.Core.ECS;

public class Universe : IDisposable
{
    public World World { get; }
    private readonly PooledList<SystemBase> _systems;

    public Universe()
    {
        World = World.Create();
        _systems = new();
    }

    public void Initialize()
    {
        foreach (var system in _systems)
        {
            if (!system.Initialize())
            {
                Log.Error("Couldn't initialize system of type {Type}", system.GetType());
                continue;
            }
            system.UpdateWatch.Start();
        }
    }

    public void Update()
    {
        foreach (var system in _systems)
        {
            system.Update(system.UpdateWatch.Elapsed);
            system.UpdateWatch.Restart();
        }
    }

    public void RegisterSystem<T>(T system) where T : SystemBase
    {
        //TODO: Replace with dependency injection
        system.ParentWorld = World;
        _systems.Add(system);
    }

    public void Dispose()
    {
        foreach (var system in _systems)
        {
            if(system is IDisposable d)
                d.Dispose();
        }
        
        _systems.Dispose();
        World.Destroy(World);
    }
}