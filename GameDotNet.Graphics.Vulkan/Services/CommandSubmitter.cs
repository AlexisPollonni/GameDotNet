using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using GameDotNet.Graphics.Vulkan.Abstractions;
using GameDotNet.Graphics.Vulkan.Tools;
using GameDotNet.Graphics.Vulkan.Tools.Extensions;
using GameDotNet.Graphics.Vulkan.Wrappers;
using Intellenum;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Silk.NET.Vulkan;

namespace GameDotNet.Graphics.Vulkan.Services;

/// <summary>
/// Opaque handle identifying a batch of command recordings.
/// Returned by <see cref="CommandSubmitter.CreateBatch"/>.
/// </summary>
public readonly record struct BatchId(int Id);

/// <summary>
/// Options controlling which queue a batch is submitted to and any GPU-side
/// wait semaphores that must be satisfied before execution begins.
/// </summary>
public sealed record BatchOptions
{
    /// <summary>
    /// Optional GPU-side wait semaphores (e.g., a swapchain image-available binary semaphore).
    /// All entries must be signaled before the batch begins executing on the GPU.
    /// </summary>
    public List<SemaphoreSubmitInfo> WaitSemaphores { get; init; } = [];
}

/// <summary>
/// Options controlling the behavior of a single <see cref="CommandRecorder"/> within a batch.
/// </summary>
public sealed record RecorderOptions
{
    /// <summary>
    /// Determines position within <c>vkCmdExecuteCommands</c>. Lower values execute earlier.
    /// Recorders sharing the same order value may execute in any relative order.
    /// </summary>
    public int ExecutionOrder { get; init; } = 0;

    /// <summary>
    /// Extra <see cref="CommandBufferUsageFlags"/> passed to <c>vkBeginCommandBuffer</c>.
    /// <see cref="CommandBufferUsageFlags.OneTimeSubmitBit"/> is always included automatically.
    /// </summary>
    public CommandBufferUsageFlags ExtraFlags { get; init; } = CommandBufferUsageFlags.None;
}

/// <summary>
/// Manages command recording and GPU submission.
/// <para>
/// Uses one <see cref="VulkanCommandBufferPool"/> per OS thread (ThreadLocal) for both
/// secondary and primary command buffers. Pools are bulk-reset lazily when the GPU has
/// finished all prior submissions that used them.
/// </para>
/// <para>
/// <see cref="SubmitAsync"/> awaits GPU completion internally — when it returns, all
/// command buffers from the batch are safe and pools will be reset on next use.
/// </para>
/// </summary>
public sealed class CommandSubmitter : IDisposable
{
    private readonly IVulkanContext _context;
    private readonly DeviceQueuesManager _queuesManager;
    private readonly GpuCompletionMonitor _completionMonitor;
    private readonly VulkanTimelineSemaphore _timelineSemaphore;

    // One pool per OS thread — permanent ownership, never shared
    private readonly ConcurrentBag<VulkanCommandBufferPool> _allPools = [];
    private readonly ThreadLocal<VulkanCommandBufferPool> _threadPools;

    // Max in-flight signal value per pool — only reset when GPU catches up
    private readonly ConcurrentDictionary<VulkanCommandBufferPool, long> _poolWatermarks = new();

    // Lightweight per-batch tracking (created in CreateBatch, removed in SubmitAsync)
    private readonly ConcurrentDictionary<int, BatchEntry> _batches = new();
    private int _nextBatchId;
    private long _nextSignalValue;

    public CommandSubmitter(
        IVulkanContext context,
        GpuCompletionMonitor completionMonitor,
        [ServiceKey] QueueFlags requiredFlags
    )
    {
        _context = context;
        _queuesManager = context.Queues;
        _completionMonitor = completionMonitor;
        _timelineSemaphore = new(context);

        _threadPools = new(() =>
        {
            var pool = new VulkanCommandBufferPool(
                context,
                (uint)_queuesManager.GetFirstQueueFamilyIndex(requiredFlags).ShouldNotBeNull()
            );
            _allPools.Add(pool);
            return pool;
        });
    }

    /// <summary>
    /// Creates a lightweight batch. No GPU resources are allocated —
    /// this just returns a grouping key for <see cref="CreateRecorder"/>
    /// and <see cref="SubmitAsync"/>.
    /// </summary>
    public BatchId CreateBatch(BatchOptions? options = null)
    {
        var id = Interlocked.Increment(ref _nextBatchId);
        _batches[id] = new(options ?? new());
        return new(id);
    }

    /// <summary>
    /// Creates a secondary <see cref="CommandRecorder"/> for the given batch.
    /// Thread-safe — each calling thread uses its own command pool.
    /// Dispose the recorder to end recording and register it with the batch.
    /// </summary>
    public CommandRecorder CreateRecorder(BatchId batch, RecorderOptions? options = null)
    {
        var opts = options ?? new();
        var pool = GetPoolWithLazyReset();
        return new(this, batch, opts.ExecutionOrder, pool, opts.ExtraFlags);
    }

    /// <summary>
    /// Builds the internal primary command buffer, submits to the GPU,
    /// and awaits GPU completion. When this returns, all work is done
    /// and pools will be bulk-reset on next use.
    /// </summary>
    public async ValueTask SubmitAsync(BatchId batch, CancellationToken ct = default)
    {
        if (!_batches.TryRemove(batch.Id, out var entry))
            throw new InvalidOperationException($"Batch {batch.Id} not found or already submitted");

        // ── Build primary (before any await — stays on calling thread) ──
        var primaryPool = GetPoolWithLazyReset();
        var primary = primaryPool.AllocateCommandBuffer();
        primary.BeginRecording();

        var secondaries = entry.GetOrderedSecondaries();
        if (secondaries.Length > 0)
            ExecuteSecondaries(primary.Underlying, secondaries);

        primary.EndRecording();

        // ── Mark watermarks so pools won't be reset while in-flight ──
        var signalValue = (ulong)Interlocked.Increment(ref _nextSignalValue);
        MarkPoolWatermarks(entry, primaryPool, signalValue);

        // ── Submit ──
        using var queueHandle = await _queuesManager.GetAvailableQueue(
            (int)primaryPool.QueueFamilyIndex,
            ct
        );

        SubmitToQueue(
            queueHandle.Queue,
            primary.Underlying,
            entry.Options.WaitSemaphores,
            signalValue
        );

        // ── Await GPU completion ──
        await _completionMonitor.WhenReached(_timelineSemaphore, signalValue);
    }

    /// <summary>Called by <see cref="CommandRecorder.Dispose"/> on the recording thread.</summary>
    internal void RegisterSecondary(BatchId batch, int order, VulkanCommandBuffer buffer)
    {
        if (_batches.TryGetValue(batch.Id, out var entry))
            entry.RegisterSecondary(order, buffer);
    }

    // ─────────────────────────── Private ───────────────────────────

    /// <summary>
    /// Returns the calling thread's pool. If all prior GPU submissions
    /// using this pool have completed, bulk-resets it first.
    /// </summary>
    private VulkanCommandBufferPool GetPoolWithLazyReset()
    {
        var pool = _threadPools.Value!;

        // Only query the semaphore if this pool has in-flight work
        if (
            _poolWatermarks.TryGetValue(pool, out var watermark)
            && _timelineSemaphore.CurrentValue >= (ulong)watermark
        )
        {
            pool.Reset();
            _poolWatermarks.TryRemove(pool, out _);
        }

        return pool;
    }

    private void MarkPoolWatermarks(
        BatchEntry entry,
        VulkanCommandBufferPool primaryPool,
        ulong signalValue
    )
    {
        var sv = (long)signalValue;
        _poolWatermarks.AddOrUpdate(primaryPool, sv, (_, existing) => Math.Max(existing, sv));

        foreach (var pool in entry.GetUniquePools())
            _poolWatermarks.AddOrUpdate(pool, sv, (_, existing) => Math.Max(existing, sv));
    }

    private unsafe void ExecuteSecondaries(CommandBuffer primary, CommandBuffer[] secondaries)
    {
        fixed (CommandBuffer* pBuffers = secondaries)
        {
            _context.Api.CmdExecuteCommands(primary, (uint)secondaries.Length, pBuffers);
        }
    }

    private void SubmitToQueue(
        DeviceQueue queue,
        CommandBuffer primaryCmd,
        List<SemaphoreSubmitInfo> waitSemaphores,
        ulong signalValue
    )
    {
        var cmdInfo = new CommandBufferSubmitInfo
        {
            SType = StructureType.CommandBufferSubmitInfo,
            CommandBuffer = primaryCmd,
        };

        var signalInfo = new SemaphoreSubmitInfo
        {
            SType = StructureType.SemaphoreSubmitInfo,
            Semaphore = _timelineSemaphore,
            Value = signalValue,
            StageMask = PipelineStageFlags2.AllCommandsBit,
        };

        queue
            .Submit(CollectionsMarshal.AsSpan(waitSemaphores), [cmdInfo], [signalInfo])
            .ThrowOnError();
    }

    public void Dispose()
    {
        foreach (var pool in _allPools)
            pool.Dispose();
        _threadPools.Dispose();
        _timelineSemaphore.Dispose();
    }

    // ─────────────────────────── BatchEntry ───────────────────────────

    /// <summary>
    /// Transient grouping of secondary command buffers for a single submission.
    /// Created in <see cref="CreateBatch"/>, consumed and removed in <see cref="SubmitAsync"/>.
    /// </summary>
    private sealed class BatchEntry(BatchOptions options)
    {
        public BatchOptions Options => options;

        private readonly ConcurrentBag<(int Order, VulkanCommandBuffer Buffer)> _secondaries = [];

        public void RegisterSecondary(int order, VulkanCommandBuffer buffer) =>
            _secondaries.Add((order, buffer));

        public CommandBuffer[] GetOrderedSecondaries() =>
            _secondaries.OrderBy(pair => pair.Order).Select(kv => kv.Buffer.Underlying).ToArray();

        public IEnumerable<VulkanCommandBufferPool> GetUniquePools() =>
            _secondaries.Select(pair => pair.Buffer.Pool).Distinct();
    }
}
