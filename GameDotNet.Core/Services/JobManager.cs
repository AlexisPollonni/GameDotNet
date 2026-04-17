using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using AutoCtor;
using dotVariant;
using GameDotNet.Core.Abstractions;
using GameDotNet.Core.Models;
using GameDotNet.Core.Tooling;
using GameDotNet.Core.Tooling.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Nito.Disposables;
using QuikGraph;
using QuikGraph.Algorithms;
using Shouldly;
using ValueTaskSupplement;
using ZLinq;

namespace GameDotNet.Core.Services;

[Variant]
[SuppressMessage("ReSharper", "DefaultStructEqualityIsUsed.Global")]
public readonly partial struct JobWorkerItem
{
    static partial void VariantOf(Startup first, Stopping second, Update third);

    public readonly struct Startup(IUpdateJob job)
    {
        public ValueTask Execute(CancellationToken token) => job.OnStarted(token);
    }

    public readonly struct Stopping(IUpdateJob job)
    {
        public ValueTask Execute(CancellationToken token) => job.OnStopped(token);
    }

    public readonly struct Update(
        IUpdateJob job,
        JobManager.JobData data,
        SceneInstanceManager sceneManager
    )
    {
        public async ValueTask Execute(CancellationToken token)
        {
            var execWatch = data.ExecuteWatch;
            var deltaWatch = data.DeltaUpdateWatch;

            execWatch.Restart();

            await job.OnUpdate(deltaWatch.Elapsed, token);
            if (job is IQueryUpdateJob queryUpdateJob)
            {
                var query = queryUpdateJob.Query;

                foreach (var scene in sceneManager.EnabledScenes)
                {
                    using var matchingEntities = scene.EntityWorld.GetEntitiesPooled(query);

                    queryUpdateJob.OnUpdateQueryEntities(deltaWatch.Elapsed, matchingEntities.Span);
                }
            }

            deltaWatch.Restart();
            data.Measure.Record(data.ExecuteWatch.Elapsed.TotalMilliseconds);
        }
    }
}

[AutoConstruct]
[RegisterSingleton<JobManager>]
public sealed partial class JobManager //TODO: for now public but change to internal later when interface is defined
    : SingleAsyncDisposable<EmptyStruct>
{
    private readonly IMeterFactory _meterFactory;
    private readonly TimeProvider _timeProvider;
    private readonly IEnumerable<IUpdateJob> _registeredJobs;
    private readonly IZeroAllocThreadPoolScheduler<JobWorkerItem> _scheduler;
    private readonly IJobDependencyGraph _jobDependencyGraph;
    private readonly SceneInstanceManager _sceneManager;

    private readonly Meter _meter;

    private bool _initialized;

    private readonly CancellationTokenSource _disposeCts = new();
    private readonly ConcurrentDictionary<IUpdateJob, JobData> _allJobs = [];
    private readonly BidirectionalGraph<IUpdateJob, SEdge<IUpdateJob>> _runningDependencyGraph =
        new(false);
    private readonly ConcurrentDictionary<IUpdateJob, ValueTask> _runningJobTasks = [];

    private readonly ConcurrentQueue<IUpdateJob> _jobsToAdd = [];
    private readonly ConcurrentQueue<IUpdateJob> _jobsToRemove = [];

    public record JobData(
        ValueStopwatch ExecuteWatch,
        ValueStopwatch DeltaUpdateWatch,
        Histogram<double> Measure
    );

    [RegisterServices]
    internal static void RegisterDependencies(IServiceCollection collection)
    {
        //TODO: make extension method for this
        collection.AddPooled<ResettableWorkItem<JobWorkerItem>>();
        collection.AddPooled<PooledValueTaskSource>();
    }

    [AutoPostConstruct]
    private void Configure(IEventRegistry registry, out Meter meter)
    {
        meter = _meterFactory.Create(new($"{typeof(JobManager).FullName}.Updates"));

        registry.OnEvent<EngineStartedEvent>(OnStartup, _disposeCts.Token);
        registry.OnEvent<EngineStoppingEvent>(OnStopping, _disposeCts.Token);
    }

    public void AddJob(IUpdateJob job)
    {
        ObjectDisposedException.ThrowIf(IsDisposeStarted, this);

        _jobsToAdd.Enqueue(job);
    }

    public void RemoveJob(IUpdateJob job)
    {
        ObjectDisposedException.ThrowIf(IsDisposeStarted, this);

        _jobsToRemove.Enqueue(job);
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    public async ValueTask Update(CancellationToken token)
    {
        ObjectDisposedException.ThrowIf(IsDisposeStarted, this);
        _initialized.ShouldBeTrue(
            "JobManager is not initialized yet. Make sure to start the engine before calling Update."
        );

        if (!_initialized)
            return;

        if (ProcessJobChanges())
        {
            RebuildRunningJobGraph();
        }

        var sharedState = (_allJobs, sceneManager: _sceneManager);
        await RunJobGraph(
                static (state, job) =>
                    new JobWorkerItem.Update(job, state._allJobs[job], state.sceneManager),
                sharedState,
                token
            )
            .ConfigureAwait(false);
    }

    protected override async ValueTask DisposeAsync(EmptyStruct context)
    {
        await _disposeCts.CancelAsync();
        _disposeCts.Dispose();

        await ValueTaskEx.WhenAll(_runningJobTasks.Values);

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

        await RunJobGraph<EmptyStruct>((_, job) => new JobWorkerItem.Startup(job), default, token)
            .ConfigureAwait(false);

        _initialized = true;
    }

    private async ValueTask OnStopping(EngineStoppingEvent _, CancellationToken token)
    {
        await RunJobGraph<EmptyStruct>((_, job) => new JobWorkerItem.Stopping(), default, token)
            .ConfigureAwait(false);

        foreach (var job in _allJobs.Keys)
        {
            job.IsStarted = false;
        }
    }

    private bool ProcessJobChanges()
    {
        var hasChanges = false;

        while (_jobsToAdd.TryDequeue(out var jobToAdd))
        {
            _allJobs.TryAdd(
                jobToAdd,
                new(
                    new(_timeProvider),
                    new(_timeProvider),
                    _meter.CreateHistogram<double>(
                        $"{jobToAdd.GetType().FullName}.ExecuteTime",
                        "ms",
                        "Execution time of the job in milliseconds"
                    )
                )
            );
            hasChanges = true;
        }

        while (_jobsToRemove.TryDequeue(out var jobToRemove))
        {
            _allJobs.TryRemove(jobToRemove, out _);
            hasChanges = true;
        }

        return hasChanges;
    }

    private void RebuildRunningJobGraph()
    {
        _runningDependencyGraph.Clear();

        var graphEdges = _allJobs
            .Keys.AsValueEnumerable()
            .SelectMany(job =>
            {
                return _jobDependencyGraph
                    .GetDependencies(job.GetType())
                    .AsValueEnumerable()
                    .Select(GetJobByType)
                    .Where(depJob => depJob.IsStarted)
                    .Select(jobDep => new SEdge<IUpdateJob>(job, jobDep));
            })
            .ToArray(); //TODO: revisit using an array pool for performance. Had to use this because bellow method only accepts arrays and pooled arrays can be bigger than requested causing errors

        _runningDependencyGraph.AddVerticesAndEdgeRange(graphEdges);

        var stoppedJobs = _allJobs.Keys.AsValueEnumerable().Where(job => !job.IsStarted);
        // Remove stopped jobs from the graph and merge dependencies
        foreach (var job in stoppedJobs)
        {
            _runningDependencyGraph.MergeVertex(
                job,
                static (source, target) => new(source, target)
            );
        }

        _runningDependencyGraph
            .IsDirectedAcyclicGraph()
            .ShouldBeTrue("Job dependency graph contains cycles");
        return;

        IUpdateJob GetJobByType(Type type)
        {
            var job = _allJobs.Keys.FirstOrDefault(j => j.GetType() == type);
            job.ShouldNotBeNull($"Job of type {type} not found in registered jobs");
            return job;
        }
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask RunJobGraph<TState>(
        Func<TState, IUpdateJob, JobWorkerItem> createWorkerItem,
        TState state,
        CancellationToken token
    )
    {
        _runningJobTasks.IsEmpty.ShouldBeTrue(
            "There are still running jobs from previous execution"
        );

        foreach (var currentJob in _runningDependencyGraph.SourceFirstTopologicalSort())
        {
            // Wait for all dependencies to complete before starting this job
            //TODO: implement prioritized scheduling to speedup uncorrelated jobs
            foreach (var depEdge in _runningDependencyGraph.InEdges(currentJob))
            {
                if (_runningJobTasks.TryRemove(depEdge.Source, out var depTask))
                {
                    await depTask.ConfigureAwait(false);
                }
            }

            // Start the current job and track its task
            var jobTask = EnqueueWorkJob(createWorkerItem(state, currentJob), token);

            _runningJobTasks.TryAdd(currentJob, jobTask);
        }

        // Wait for all jobs to complete
        while (!_runningJobTasks.IsEmpty)
        {
            if (_runningJobTasks.TryRemove(_runningJobTasks.First().Key, out var remainingTask))
            {
                await remainingTask.ConfigureAwait(false);
            }
        }
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask EnqueueWorkJob(JobWorkerItem workItem, CancellationToken token)
    {
        await _scheduler.EnqueueWork(OnWorkerItemExecute, workItem, token).ConfigureAwait(false);
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private static async ValueTask OnWorkerItemExecute(
        JobWorkerItem workItem,
        CancellationToken token
    )
    {
        if (workItem.TryMatch(out JobWorkerItem.Startup startup))
        {
            await startup.Execute(token).ConfigureAwait(false);
        }
        else if (workItem.TryMatch(out JobWorkerItem.Stopping stopping))
        {
            await stopping.Execute(token).ConfigureAwait(false);
        }
        else if (workItem.TryMatch(out JobWorkerItem.Update update))
        {
            await update.Execute(token).ConfigureAwait(false);
        }
        else
            throw new InvalidOperationException();
    }
}
