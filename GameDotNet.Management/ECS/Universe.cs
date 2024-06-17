using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Collections.Pooled;
using GameDotNet.Core.Tools.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Schedulers;
using ValueTaskSupplement;

namespace GameDotNet.Management.ECS;

internal struct SystemEntry
{
    public SystemEntry(SystemBase system, Meter meter)
    {
        System = system;
        UpdateWatch = new();

        var typeName = system.GetType().Name;

        var updateMeasure = meter.CreateHistogram<double>($"{typeName}.Update", unit: "ms");

        UpdateJob = new UpdateExecute(system, new(), UpdateWatch, updateMeasure);

        IsRunning = false;
    }

    public SystemBase System { get; }
    public Stopwatch UpdateWatch { get; }
    public IJob UpdateJob { get; }
    public bool IsRunning { get; internal set; }

    private class UpdateExecute(SystemBase system, Stopwatch executeSw, Stopwatch deltaUpdateSw, Histogram<double> measure)
        : IJob
    {
        public void Execute()
        {
            executeSw.Restart();
            system.Update(deltaUpdateSw.Elapsed);
            deltaUpdateSw.Restart();
            measure.Record(executeSw.Elapsed.TotalMilliseconds);
        }
    }
}

public sealed class Universe : IDisposable
{
    private readonly Meter _meter;
    private readonly ILogger<Universe> _logger;
    private readonly IServiceProvider _provider;
    private readonly PriorityQueue<SystemBase, int> _updateQueue;

    private JobScheduler _scheduler = null!;
    private Dictionary<SystemBase, SystemEntry> _systemEntries;

    private bool _initialized;
    private bool _disposed;

    public Universe(ILogger<Universe> logger, IMeterFactory meterFactory, IServiceProvider provider)
    {
        _logger = logger;
        _provider = provider;
        _meter = meterFactory.Create(new("Universe.Updates"));
        _systemEntries = new();
        _updateQueue = new();
    }

    public void AddSystem(SystemBase system)
    {
        lock (_systemEntries)
        {
            _systemEntries.Add(system, new(system, _meter));
        }
    }

    public void RemoveSystem(SystemBase system)
    {
        lock (_systemEntries)
        {
            _systemEntries.Remove(system);
        }
    }

    public async Task Initialize(CancellationToken token = default)
    {
        if (_initialized) return;
        
        _scheduler = new(new() { ThreadPrefixName = "Universe.Update" });

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
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_initialized) return;

        using var mainPassEntries = new PooledList<(int, IJob)>(_systemEntries.Count);

        lock (_systemEntries)
        {
            PopulateQueueForUpdate();

            while (_updateQueue.TryDequeue(out var system, out var priority))
            {
                ref var entry = ref GetEntry(system);

                mainPassEntries.Add((priority, entry.UpdateJob));
            }
        }

        var lastPass = ScheduleExecutePass(mainPassEntries.Span);


        _scheduler.Flush();
        lastPass?.Complete();

        return;
    }

    private ref SystemEntry GetEntry(SystemBase system)
    {
        ref var entry = ref CollectionsMarshal.GetValueRefOrNullRef(_systemEntries, system);

        if (Unsafe.IsNullRef(ref entry)) throw new InvalidOperationException();

        return ref entry;
    }

    private JobHandle? ScheduleExecutePass(ReadOnlySpan<(int Priority, IJob Job)> jobs, JobHandle? dependency = null)
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
                if (handles.Count > 0) lastDep = _scheduler.CombineDependencies(handles.Span);
                handles.Clear();
            }

            var h = _scheduler.Schedule(job, lastDep);

            handles.Add(h);
        }

        return handles.Count > 0 && lastDep is not null ? _scheduler.CombineDependencies(handles.Span) : null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _scheduler.Dispose();
        _meter.Dispose();
        foreach (var system in _systemEntries.Keys) system.DisposeIf();
    }

    private void RetrieveSystems()
    {
        var systems = _provider.GetServices<SystemBase>();
        lock (_systemEntries)
        {
            _systemEntries = systems.Select(s => new SystemEntry(s, _meter)).ToDictionary(entry => entry.System);
        }
    }

    private void PopulateQueueForUpdate()
    {
        foreach (var system in _systemEntries.Keys)
        {
            ref var entry = ref GetEntry(system);

            var sRunning = system.IsRunning;
            if (entry.IsRunning != sRunning)
            {
                system.OnRunningStatusChanged(sRunning);
                entry.IsRunning = sRunning;
            }

            if (entry.UpdateWatch.Elapsed < system.Description.UpdateThrottle) continue;

            if (system.IsInitialized && system.IsRunning) _updateQueue.Enqueue(system, system.Description.Priority);
        }
    }
}