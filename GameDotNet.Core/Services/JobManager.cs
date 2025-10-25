using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using Arch.Core;
using Collections.Pooled;
using CommunityToolkit.HighPerformance.Buffers;
using GameDotNet.Core.Abstractions;
using GameDotNet.Core.Tooling;
using MessagePipe;
using Microsoft.Extensions.ObjectPool;
using QuikGraph;
using QuikGraph.Algorithms;
using Shouldly;
using ZLinq;

namespace GameDotNet.Core.Services;

public sealed class JobManager : IAsyncDisposable
{
    private readonly SceneManager _sceneManager;
    private readonly Meter _meter;

    private bool _initialized;
    private bool _disposed;

    private readonly ThreadPoolJobScheduler<WorkerItem> _scheduler;

    private readonly ConcurrentDictionary<IUpdateJob, JobData> _allJobs = [];
    private readonly BidirectionalGraph<IUpdateJob, SEdge<IUpdateJob>> _runningDependencyGraph = new(false);

    private readonly ConcurrentQueue<IUpdateJob> _jobsToAdd = [];
    private readonly ConcurrentQueue<IUpdateJob> _jobsToRemove = [];

    private readonly IEnumerable<IUpdateJob> _registeredJobs;
    private readonly IDisposable _startupSubscription;
    private readonly IDisposable _stoppingSubscription;
    private readonly TimeProvider _timeProvider;
    private readonly IJobDependencyGraph _jobDependencyGraph;
    public JobManager(IMeterFactory meterFactory,
                      TimeProvider timeProvider,
                      IAsyncSubscriber<EngineStartedEvent> engineStart,
                      IAsyncSubscriber<EngineStoppingEvent> engineStopping,
                      IEnumerable<IUpdateJob> registeredJobs,
                      ObjectPool<PooledValueTaskSource> valueTaskSourcePool,
                      IJobDependencyGraph jobDependencyGraph,
                      SceneManager sceneManager)
    {
        _timeProvider = timeProvider;
        _registeredJobs = registeredJobs;
        _jobDependencyGraph = jobDependencyGraph;
        _sceneManager = sceneManager;
        _meter = meterFactory.Create(new($"{typeof(JobManager).FullName}.Updates"));
        _scheduler = new(valueTaskSourcePool);
        _startupSubscription = engineStart.Subscribe(OnStartup);
        _stoppingSubscription = engineStopping.Subscribe(OnStopping);
    }

    private readonly record struct WorkerItem(IUpdateJob Job, WorkerItem.ItemType Type, JobManager SrcManager, JobData Data)
    {
        internal enum ItemType
        {
            Startup,
            Stopping,
            Update
        }
    }

    private readonly record struct JobData(
        ValueStopwatch ExecuteWatch,
        ValueStopwatch DeltaUpdateWatch,
        Histogram<double> Measure);

    public void AddJob(IUpdateJob job)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        _jobsToAdd.Enqueue(job);
    }

    public void RemoveJob(IUpdateJob job)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        _jobsToRemove.Enqueue(job);
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    public async ValueTask Update(CancellationToken token)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_initialized) return;

        if (ProcessJobChanges())
        {
            RebuildRunningJobGraph();
        }

        await RunJobGraph(WorkerItem.ItemType.Update, token);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        
        _startupSubscription.Dispose();
        _stoppingSubscription.Dispose();

        await _scheduler.DisposeAsync();
        _meter.Dispose();
    }

    private async ValueTask OnStartup(EngineStartedEvent _, CancellationToken token)
    {
        foreach (var job in _registeredJobs)
        {
            AddJob(job);
        }

        if (ProcessJobChanges())
        {
            foreach (var job in _allJobs.Keys)
            {
                if (job.Options.StartsWithEngine)
                {
                    job.IsStarted = true;
                }
            }
            
            RebuildRunningJobGraph();
        }

        _scheduler.StartWorkers();

        await RunJobGraph(WorkerItem.ItemType.Startup, token);
        
        _initialized = true;
    }

    private async ValueTask OnStopping(EngineStoppingEvent _, CancellationToken token)
    {
        await RunJobGraph(WorkerItem.ItemType.Stopping, token);
        
        foreach (var job in _allJobs.Keys)
        {
            job.IsStarted = false;
        }
    }

    private bool ProcessJobChanges()
    {
        if (_jobsToAdd.IsEmpty && _jobsToRemove.IsEmpty)
        {
            return false;
        }
        
        while (_jobsToAdd.TryDequeue(out var jobToAdd))
        {
            _allJobs.TryAdd(jobToAdd, new(
                new(_timeProvider),
                new(_timeProvider),
                _meter.CreateHistogram<double>($"{jobToAdd.GetType().FullName}.ExecuteTime", "ms", "Execution time of the job in milliseconds")));
        }

        while (_jobsToRemove.TryDequeue(out var jobToRemove))
        {
            _allJobs.TryRemove(jobToRemove, out _);
        }

        return true;
    }

    private void RebuildRunningJobGraph()
    {
        _runningDependencyGraph.Clear();

        using var graphEdges = _allJobs.Keys.AsValueEnumerable()
                                       .SelectMany(job =>
                                       {
                                           return _jobDependencyGraph.GetDependencies(job.GetType())
                                                                    .AsValueEnumerable()
                                                                    .Select(GetJobByType)
                                                                    .Where(depJob => depJob.IsStarted)
                                                                    .Select(jobDep => new SEdge<IUpdateJob>(
                                                                                job,
                                                                                jobDep));
                                       })
                                       .ToArrayPool();
        
        _runningDependencyGraph.AddVerticesAndEdgeRange(graphEdges.Array);

        var stoppedJobs = _allJobs.Keys.AsValueEnumerable().Where(job => !job.IsStarted);
        // Remove stopped jobs from the graph and merge dependencies
        foreach (var job in stoppedJobs)
        {
            _runningDependencyGraph.MergeVertex(job, static (source, target) => new(source, target));
        }

        _runningDependencyGraph.IsDirectedAcyclicGraph().ShouldBeTrue("Job dependency graph contains cycles");
        return;

        IUpdateJob GetJobByType(Type type)
        {
            var job = _allJobs.Keys.FirstOrDefault(j => j.GetType() == type);
            job.ShouldNotBeNull($"Job of type {type} not found in registered jobs");
            return job;
        }
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask RunJobGraph(WorkerItem.ItemType updateType, CancellationToken token)
    {
        using var runningTasks = new PooledDictionary<IUpdateJob, ValueTask>();
    
        foreach (var currentJob in _runningDependencyGraph.SourceFirstTopologicalSort())
        {
            // Wait for all dependencies to complete before starting this job
            //TODO: implement prioritized scheduling to speedup uncorrelated jobs
            foreach (var depEdge in _runningDependencyGraph.InEdges(currentJob))
            {
                if (!runningTasks.TryGetValue(depEdge.Source, out var depTask)) continue;
                
                await depTask;
                runningTasks.Remove(depEdge.Source); // Remove completed dependency task since we know it executed
            }
        
            // Start the current job and track its task
            var jobTask = EnqueueWorkJob(currentJob, updateType, token);
            runningTasks[currentJob] = jobTask;
        }
    
        // Wait for all jobs to complete
        if (runningTasks.Count > 0)
        {
            await PooledValueTaskSource.WhenAll(runningTasks.Values);
        }
    }


    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask EnqueueWorkJob(IUpdateJob job, WorkerItem.ItemType type, CancellationToken token)
    {
        var workItem = new WorkerItem(job, type, this, _allJobs[job]);
        await _scheduler.EnqueueWork(WorkerLoop, workItem, token);
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private static async ValueTask WorkerLoop(WorkerItem workItem, CancellationToken token)
    {
        var job = workItem.Job;
        switch (workItem.Type)
        {
            case WorkerItem.ItemType.Startup:
                await job.OnStarted(token);
                break;
            case WorkerItem.ItemType.Stopping:
                await job.OnStopped(token);
                break;
            case WorkerItem.ItemType.Update:
                var jobData = workItem.Data;
                
                jobData.ExecuteWatch.Restart();
                
                await job.OnUpdate(jobData.DeltaUpdateWatch.Elapsed, token);
                if (workItem.Job is IQueryUpdateJob queryUpdateJob)
                {
                    var query = queryUpdateJob.Query;
                    // TODO: For now we just get the world from the scene manager, later we might want to support multiple worlds/scenes
                    using var matchingEntities = GetMatchingEntities(workItem.SrcManager._sceneManager.World, in query);
                }

                jobData.DeltaUpdateWatch.Restart();
                jobData.Measure.Record(jobData.ExecuteWatch.Elapsed.TotalMilliseconds);

                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static SpanOwner<Entity> GetMatchingEntities(World world, in QueryDescription query)
    {
        var matchCount = world.CountEntities(query);
        var matches = SpanOwner<Entity>.Allocate(matchCount);
        world.GetEntities(query, matches.Span);

        return matches;
    }
}