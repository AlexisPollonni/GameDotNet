using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using GameDotNet.Core.Tooling;
using Microsoft.Extensions.ObjectPool;
using Shouldly;

namespace GameDotNet.Core.Services;

public sealed class ThreadPoolJobScheduler<TState>(ObjectPool<PooledValueTaskSource> valueTaskSourcePool) : IAsyncDisposable
    where TState : struct
{
    //TODO: Consider prioritized channels for higher concurrency
    private readonly Channel<JobItem> _channel = Channel.CreateUnbounded<JobItem>(new()
    {
        SingleWriter = true,
        SingleReader = false
    });

    private bool _isDisposed;
    private ImmutableArray<Task> _workers = [];
    private readonly CancellationTokenSource _cts = new();

    public void StartWorkers()
    {
        _workers.ShouldBeEmpty("The workers have already been started.");
        
        var workerCount = Environment.ProcessorCount;
        _workers = _workers.AddRange(CreateWorkers(workerCount, _cts.Token));
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    public async ValueTask EnqueueWork(Func<TState, CancellationToken, ValueTask> work,
                                       TState userState,
                                       CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        work.ShouldNotBeNull();

        var src = valueTaskSourcePool.Get();

        var jobItem = new JobItem(work, userState, src, token);

        await _channel.Writer.WriteAsync(jobItem, token);

        try
        {
            await src.AsTask();
        }
        finally
        {
            valueTaskSourcePool.Return(src);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        
        _channel.Writer.Complete();
        _isDisposed = true;

        await _cts.CancelAsync();

        try
        {
            await Task.WhenAll(_workers).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation exceptions during disposal
        }

        _cts.Dispose();
    }

    private IEnumerable<Task> CreateWorkers(int workerCount, CancellationToken cancellationToken)
    {
        return Enumerable.Repeat((this, cancellationToken), workerCount)
                         .Select(pair => Task.Factory.StartNew(WorkerLoop,
                                                               pair.Item1,
                                                               pair.cancellationToken,
                                                               TaskCreationOptions.LongRunning,
                                                               TaskScheduler.Default));
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private static async Task WorkerLoop(object? state)
    {
        var scheduler = state.ShouldBeOfType<ThreadPoolJobScheduler<TState>>();

        var channelReader = scheduler._channel.Reader;

        await foreach (var jobItem in channelReader.ReadAllAsync(scheduler._cts.Token))
        {
            try
            {
                await jobItem.Work(jobItem.UserState, jobItem.Token).ConfigureAwait(false);
                jobItem.Source.SetResult();
            }
            catch (Exception ex)
            {
                jobItem.Source.SetException(ex);
            }
        }
    }

    private readonly record struct JobItem(
        Func<TState, CancellationToken, ValueTask> Work,
        TState UserState,
        PooledValueTaskSource Source,
        CancellationToken Token);
}