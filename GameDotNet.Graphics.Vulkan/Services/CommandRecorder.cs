using GameDotNet.Graphics.Vulkan.Abstractions;
using GameDotNet.Graphics.Vulkan.Wrappers;
using Silk.NET.Vulkan;

namespace GameDotNet.Graphics.Vulkan.Services;

/// <summary>
/// A lightweight ref struct wrapping a secondary command buffer.
/// Cannot be stored or captured — enforces single-thread, single-scope recording.
/// <para>
/// Dispose to end recording and register the buffer with its parent batch.
/// With dynamic rendering, call <c>CmdBeginRendering</c>/<c>CmdEndRendering</c> directly
/// on this recorder — no inheritance info or <see cref="CommandBufferUsageFlags.RenderPassContinueBit"/> needed.
/// </para>
/// </summary>
public readonly ref struct CommandRecorder : IVulkanWrapper<CommandBuffer>, IDisposable
{
    private readonly CommandSubmitter _submitter;
    private readonly BatchId _batch;
    private readonly int _executionOrder;

    public VulkanCommandBuffer Buffer { get; }
    public IVulkanContext Context => Buffer.Context;
    public CommandBuffer Underlying => Buffer.Underlying;

    internal unsafe CommandRecorder(
        CommandSubmitter submitter,
        BatchId batch,
        int executionOrder,
        VulkanCommandBufferPool pool,
        CommandBufferUsageFlags extraFlags
    )
    {
        _submitter = submitter;
        _batch = batch;
        _executionOrder = executionOrder;

        Buffer = pool.AllocateCommandBuffer(CommandBufferLevel.Secondary);
        Buffer.BeginRecording(
            CommandBufferUsageFlags.OneTimeSubmitBit | extraFlags,
            [new(queryFlags: QueryControlFlags.None)]
        );
    }

    /// <summary>
    /// Ends recording and registers this secondary buffer with the parent batch for submission.
    /// </summary>
    public void Dispose()
    {
        Buffer.EndRecording();
        _submitter.RegisterSecondary(_batch, _executionOrder, Buffer);
    }
}
