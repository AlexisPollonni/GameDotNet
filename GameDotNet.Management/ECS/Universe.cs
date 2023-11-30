using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Arch.Core;
using Arch.Core.Extensions;
using Collections.Pooled;
using GameDotNet.Core.Physics.Components;
using GameDotNet.Core.Tools.Extensions;
using GameDotNet.Graphics.Assets;
using GameDotNet.Management.ECS.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Schedulers;
using ValueTaskSupplement;

namespace GameDotNet.Management.ECS;

internal readonly struct SystemEntry
{
    public SystemEntry(SystemBase system, Meter meter)
    {
        System = system;
        UpdateWatch = new();

        var typeName = system.GetType().Name;
        
        var beforeMeasure = meter.CreateHistogram<double>($"{typeName}.BeforeUpdate", unit: "millisecond");
        var updateMeasure = meter.CreateHistogram<double>($"{typeName}.Update", unit: "millisecond");
        var afterMeasure = meter.CreateHistogram<double>($"{typeName}.AfterUpdate", unit: "millisecond");

        BeforeUpdateJob = new BeforeUpdateExecute(system, new(), beforeMeasure);
        UpdateJob = new UpdateExecute(system, new(), UpdateWatch, updateMeasure);
        AfterUpdateJob = new AfterUpdateExecute(system, new(), afterMeasure);
    }

    public SystemBase System { get; }
    public Stopwatch UpdateWatch { get; }
    
    public IJob BeforeUpdateJob { get; }
    public IJob UpdateJob { get; }
    public IJob AfterUpdateJob { get; }
    
    
    private class BeforeUpdateExecute(SystemBase system, Stopwatch executeSw, Histogram<double> measure) : IJob
    {
        public void Execute()
        {
            executeSw.Restart();
            system.BeforeUpdate();
            measure.Record(executeSw.Elapsed.TotalMilliseconds);
        }
    }
    private class UpdateExecute(SystemBase system, Stopwatch executeSw, Stopwatch deltaUpdateSw, Histogram<double> measure) : IJob
    {
        public void Execute()
        {
            executeSw.Restart();
            system.Update(deltaUpdateSw.Elapsed);
            deltaUpdateSw.Restart();
            measure.Record(executeSw.Elapsed.TotalMilliseconds);
        }
    }
    private class AfterUpdateExecute(SystemBase system, Stopwatch executeSw, Histogram<double> measure) : IJob
    {
        public void Execute()
        {
            executeSw.Restart();
            system.AfterUpdate();
            measure.Record(executeSw.Elapsed.TotalMilliseconds);
        }
    }
}

public sealed class Universe : IDisposable
{
    public World World { get; }

    private readonly Meter _meter;
    private readonly ILogger<Universe> _logger;
    private readonly IServiceProvider _provider;
    private readonly PriorityQueue<SystemBase, int> _updateQueue;
    private readonly PooledList<EntityReference> _loadedSceneEntities;
    private JobScheduler? _scheduler;
    private Dictionary<SystemBase, SystemEntry> _systemEntries;
    

    private bool _initialized;
    private Scene? _loadedScene;

    public Universe(ILogger<Universe> logger, IMeterFactory meterFactory, IServiceProvider provider)
    {
        _logger = logger;
        _provider = provider;
        World = World.Create();

        _meter = meterFactory.Create(new("Universe.Updates"));
        _systemEntries = new();
        _updateQueue = new();
        _loadedSceneEntities = new();
    }

    public async Task Initialize(CancellationToken token = default)
    {
        RetrieveSystems();
        _updateQueue.EnqueueRange(_systemEntries.Keys.Select(sys => (sys, sys.Description.Priority)));

        using var taskList = new PooledList<ValueTask<bool>>();
        var currentPriority = 0;
        while (_updateQueue.TryDequeue(out var system, out var priority))
        {
            token.ThrowIfCancellationRequested();

            taskList.Add(InitSystem(system, token));

            if (priority == currentPriority) continue;
            
            currentPriority = priority;

            await ValueTaskEx.WhenAll(taskList);
            taskList.Clear();
        }

        _initialized = true;
        return;

        async ValueTask<bool> InitSystem(SystemBase system, CancellationToken t)
        {
            var res = await system.Initialize(t);

            system.IsInitialized = res;
            if (!res)
            {
                _logger.LogError("Couldn't initialize system of type {Type}", system.GetType());
                return res;
            }
            
            
            if (system.Description.StartAfterInitialization)
            {
                system.IsRunning = true;
                _systemEntries[system].UpdateWatch.Start();
            }
            
            return res;
        }
    }

    public void Update()
    {
        if (!_initialized) return;

        _scheduler ??= _provider.GetRequiredService<JobScheduler>();

        using var beforePassEntries = new PooledList<(int, IJob)>(_systemEntries.Count);
        using var mainPassEntries = new PooledList<(int, IJob)>(_systemEntries.Count);
        using var afterPassEntries = new PooledList<(int, IJob)>(_systemEntries.Count);
        
        PopulateQueueForUpdate();

        while (_updateQueue.TryDequeue(out var system, out var priority))
        {
            ref var entry = ref GetEntry(system);
            
            beforePassEntries.Add((priority, entry.BeforeUpdateJob));
            mainPassEntries.Add((priority, entry.UpdateJob));
            afterPassEntries.Add((priority, entry.AfterUpdateJob));
        }

        var lastPass = ScheduleExecutePass(beforePassEntries.Span);
        lastPass = ScheduleExecutePass(mainPassEntries.Span, lastPass);
        lastPass = ScheduleExecutePass(afterPassEntries.Span, lastPass);

        _scheduler.Flush();
        lastPass?.Complete();

        return;

        ref SystemEntry GetEntry(SystemBase system)
        {
            ref var entry = ref CollectionsMarshal.GetValueRefOrNullRef(_systemEntries, system);

            if (Unsafe.IsNullRef(ref entry)) throw new InvalidOperationException();

            return ref entry;
        }

        JobHandle? ScheduleExecutePass(ReadOnlySpan<(int Priority, IJob Job)> jobs, JobHandle? dependency = null)
        {
            var currentPriority = -1;
            var lastDep = dependency;
            using var handles = new PooledList<JobHandle>(_updateQueue.Count);
            
            foreach (var (priority, job) in jobs)
            {
                if (currentPriority != priority)
                {
                    if (currentPriority > priority) throw new("This should not happen");
                    
                    currentPriority = priority;
                    if(handles.Count > 0)
                        lastDep = _scheduler.CombineDependencies(handles.Span);
                    handles.Clear();
                }

                var h = _scheduler.Schedule(job, lastDep);

                handles.Add(h);
            }

            return handles.Count > 0 && lastDep is not null ? _scheduler.CombineDependencies(handles.Span) : null;
        }
    }

    public bool LoadScene(Scene scene)
    {
        // Make sure scene is unloaded before loading another
        UnloadScene();

        CreateFromSceneObject(scene.Root, new());

        return true;
    }

    public void UnloadScene()
    {
        foreach (ref var entity in _loadedSceneEntities.Span)
            if (entity.IsAlive())
                World.Destroy(entity.Entity);

        _loadedSceneEntities.Clear();
        _loadedScene = null;
    }

    public void Dispose()
    {
        foreach (var system in _systemEntries.Keys) system.DisposeIf();

        _loadedSceneEntities.Dispose();
        World.Destroy(World);
    }

    private void RetrieveSystems()
    {
        var systems = _provider.GetServices<SystemBase>();
        _systemEntries = systems.Select(s => new SystemEntry(s, _meter)).ToDictionary(entry => entry.System);
    }
    
    private void CreateFromSceneObject(SceneObject obj, in Transform accTransform)
    {
        var transform = accTransform * obj.Transform;

        foreach (var meshes in obj.Meshes.WithIndex())
        {
            var e = World.Create(new Tag($"{obj.Name}_{meshes.Index}"),
                                 meshes.Item,
                                 transform.ToTranslation(),
                                 transform.ToRotation(),
                                 transform.ToScale());

            _loadedSceneEntities.Add(e.Reference());
        }

        foreach (var child in obj.Children)
        {
            CreateFromSceneObject(child, transform);
        }
    }

    private void PopulateQueueForUpdate()
    {
        foreach (var (system, entry) in _systemEntries)
        {
            if(system.IsInitialized && system.IsRunning)
                _updateQueue.Enqueue(system, system.Description.Priority);
        }
    }
}