using System.Threading.Channels;
using CommunityToolkit.HighPerformance;
using GameDotNet.Core.Tooling;
using GameDotNet.Graphics.Vulkan.Wrappers;
using Microsoft.Extensions.ObjectPool;
using Nito.Disposables;
using Silk.NET.Vulkan;
using ZLinq;

namespace GameDotNet.Graphics.Vulkan.Services;

/// <summary>
/// Single background thread that monitors all pending timeline semaphore completions.
/// Uses vkWaitSemaphores with AnyBit — kernel wakes the thread on the first GPU signal.
/// </summary>
public sealed class GpuCompletionMonitor : SingleAsyncDisposable<EmptyStruct>
{
    private readonly ObjectPool<PooledValueTaskSource> _vtsPool;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loopTask;

    // New entries are written to this channel from any thread; the monitor thread reads them
    private readonly Channel<PendingCompletion> _incoming =
        Channel.CreateUnbounded<PendingCompletion>(new() { SingleReader = true });

    private readonly record struct PendingCompletion(
        VulkanTimelineSemaphore Semaphore,
        ulong TargetValue,
        PooledValueTaskSource Source
    );

    public GpuCompletionMonitor(ObjectPool<PooledValueTaskSource> vtsPool)
        : base(default)
    {
        _vtsPool = vtsPool;

        _loopTask = Task.Factory.StartNew(MonitorLoop, TaskCreationOptions.LongRunning);
    }

    /// <summary>
    /// Register a pending completion. Returns a ValueTask that completes when
    /// the GPU signals <paramref name="semaphore"/> at ≥ <paramref name="targetValue"/>.
    /// Thread-safe — can be called from any thread.
    /// </summary>
    public ValueTask WhenReached(VulkanTimelineSemaphore semaphore, ulong targetValue)
    {
        var vts = _vtsPool.Get();
        _incoming.Writer.TryWrite(new(semaphore, targetValue, vts));
        return vts.AsValueTask();
    }

    private async Task MonitorLoop()
    {
        var semaphores = new List<VulkanTimelineSemaphore>();
        var values = new List<ulong>();
        List<PendingCompletion> tracked = [];

        while (!IsDisposeStarted && !_cts.IsCancellationRequested)
        {
            // Drain all newly registered completions into the tracked list
            await foreach (var pendingCompletion in _incoming.Reader.ReadAllAsync(_cts.Token))
            {
                tracked.Add(pendingCompletion);
                semaphores.Add(pendingCompletion.Semaphore);
                values.Add(pendingCompletion.TargetValue);
            }

            if (tracked.Count == 0)
            {
                continue;
            }

            var result = VulkanTimelineSemaphore.WaitAny(
                semaphores.AsSpan(),
                values.AsSpan(),
                TimeSpan.FromMilliseconds(1)
            );

            // Timeout is fine — just loop and re-check
            if (result is not (Result.Success or Result.Timeout))
                continue; // or log error

            // Check which ones actually completed — non-blocking reads
            foreach (
                var (index, pendingCompletion) in tracked
                    .AsValueEnumerable()
                    .Where(static completion =>
                        completion.Semaphore.CurrentValue >= completion.TargetValue
                    )
                    .Index()
                    .Reverse()
            )
            {
                // Completed — signal the awaiter and return VTS to pool
                pendingCompletion.Source.SetResult();
                _vtsPool.Return(pendingCompletion.Source);

                // swap-remove would be faster for large lists
                tracked.RemoveAt(index);
                semaphores.RemoveAt(index);
                values.RemoveAt(index);
            }
        }
    }

    protected override async ValueTask DisposeAsync(EmptyStruct context)
    {
        _incoming.Writer.Complete();
        await _cts.CancelAsync();

        try
        {
            await _loopTask;
        }
        catch (OperationCanceledException)
        {
            // Expected on cancellation, ignore
        }
    }
}
