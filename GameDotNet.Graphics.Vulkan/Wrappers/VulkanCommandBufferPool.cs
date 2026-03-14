using CommunityToolkit.HighPerformance;
using GameDotNet.Graphics.Vulkan.Abstractions;
using GameDotNet.Graphics.Vulkan.Tools.Extensions;
using Silk.NET.Vulkan;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace GameDotNet.Graphics.Vulkan.Wrappers;

public sealed class VulkanCommandBufferPool : IVulkanWrapper<CommandPool>, IDisposable
{
    public IVulkanContext Context { get; }
    public CommandPool Underlying { get; }

    private readonly DeviceQueue _queue;

    private readonly List<CommandBuffer> _usedCommandBuffers = [];
    private readonly Lock _lock = new();

    public VulkanCommandBufferPool(IVulkanContext context, DeviceQueue queue)
    {
        Context = context;
        _queue = queue;

        var commandPoolCreateInfo = new CommandPoolCreateInfo
        {
            SType = StructureType.CommandPoolCreateInfo,
            Flags = CommandPoolCreateFlags.TransientBit,
            QueueFamilyIndex = (uint)queue.FamilyIndex,
        };

        context
            .Api.CreateCommandPool(
                context.Device,
                in commandPoolCreateInfo,
                in context.Callbacks.Handle,
                out var pool
            )
            .ThrowOnError();

        Underlying = pool;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            FreeUsedCommandBuffers();
            Context.Api.DestroyCommandPool(Context.Device, Underlying, in Context.Callbacks.Handle);
        }
    }

    public VulkanCommandBuffer CreateCommandBuffer(VulkanFence? fence = null)
    {
        return new(Context, fence, _queue);
    }

    public void FreeUsedCommandBuffers()
    {
        lock (_lock)
        {
            if (_usedCommandBuffers.Count == 0)
            {
                return;
            }

            var s = _usedCommandBuffers.AsSpan();
            Context.Api.FreeCommandBuffers(Context.Device, Underlying, (uint)s.Length, s);

            _usedCommandBuffers.Clear();
        }
    }

    private CommandBuffer AllocateCommandBuffer()
    {
        var commandBufferAllocateInfo = new CommandBufferAllocateInfo
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = Underlying,
            CommandBufferCount = 1,
            Level = CommandBufferLevel.Primary,
        };

        lock (_lock)
        {
            Context.Api.AllocateCommandBuffers(
                Context.Device,
                in commandBufferAllocateInfo,
                out var commandBuffer
            );

            return commandBuffer;
        }
    }

    private void DisposeCommandBuffer(VulkanCommandBuffer commandBuffer)
    {
        lock (_lock)
        {
            _usedCommandBuffers.Add(commandBuffer);
        }
    }

    public sealed class VulkanCommandBuffer : IVulkanWrapper<CommandBuffer>, IDisposable
    {
        public IVulkanContext Context { get; }
        public CommandBuffer Underlying { get; }
        public VulkanFence Fence { get; }

        private readonly DeviceQueue _queue;
        private readonly bool _fenceExternal;

        private bool _hasEnded;
        private bool _hasStarted;

        internal VulkanCommandBuffer(IVulkanContext context, VulkanFence? fence, DeviceQueue queue)
        {
            _fenceExternal = fence is not null;
            Context = context;
            _queue = queue;

            Fence =
                fence
                ?? new(
                    context.Api,
                    context.Device,
                    FenceCreateFlags.SignaledBit,
                    callbacks: context.Callbacks.WithUserData("CmdBufferPool::Fence")
                );

            Underlying = context.Pool.AllocateCommandBuffer();
        }

        public static implicit operator CommandBuffer(VulkanCommandBuffer buffer) =>
            buffer.Underlying;

        public void Dispose()
        {
            Fence.Wait();
            lock (Context.Pool._lock)
            {
                Context.Api.FreeCommandBuffers(
                    Context.Device,
                    Context.Pool.Underlying,
                    1,
                    [Underlying]
                );
            }

            if (!_fenceExternal)
                Fence.Dispose();
        }

        public void BeginRecording()
        {
            if (_hasStarted)
                return;

            Fence.Wait();
            Fence.Reset();

            var beginInfo = new CommandBufferBeginInfo
            {
                SType = StructureType.CommandBufferBeginInfo,
                Flags = CommandBufferUsageFlags.OneTimeSubmitBit,
            };

            Context.Api.BeginCommandBuffer(Underlying, in beginInfo);

            _hasStarted = true;
        }

        public void EndRecording()
        {
            if (!_hasStarted || _hasEnded)
                return;

            _hasEnded = true;

            Context.Api.EndCommandBuffer(Underlying);
        }

        public void Submit(
            VulkanSemaphore? wait = null,
            PipelineStageFlags? waitDstStageMask = null,
            VulkanSemaphore? signal = null
        )
        {
            ReadOnlySpan<Semaphore> w = wait is null ? null : stackalloc[] { wait.Handle };
            ReadOnlySpan<PipelineStageFlags> f = waitDstStageMask is null
                ? null
                : stackalloc[] { waitDstStageMask.Value };
            ReadOnlySpan<Semaphore> sig = signal is null ? null : stackalloc[] { signal.Handle };

            Submit(w, f, sig);
        }

        private unsafe void Submit(
            ReadOnlySpan<Semaphore> waitSemaphores,
            ReadOnlySpan<PipelineStageFlags> waitDstStageMask,
            ReadOnlySpan<Semaphore> signalSemaphores
        )
        {
            EndRecording();

            fixed (
                Semaphore* pWaitSemaphores = waitSemaphores,
                    pSignalSemaphores = signalSemaphores
            )
            {
                fixed (PipelineStageFlags* pWaitDstStageMask = waitDstStageMask)
                {
                    var commandBuffer = Underlying;
                    var submitInfo = new SubmitInfo
                    {
                        SType = StructureType.SubmitInfo,
                        WaitSemaphoreCount = !waitSemaphores.IsEmpty
                            ? (uint)waitSemaphores.Length
                            : 0,
                        PWaitSemaphores = pWaitSemaphores,
                        PWaitDstStageMask = pWaitDstStageMask,
                        CommandBufferCount = 1,
                        PCommandBuffers = &commandBuffer,
                        SignalSemaphoreCount = !signalSemaphores.IsEmpty
                            ? (uint)signalSemaphores.Length
                            : 0,
                        PSignalSemaphores = pSignalSemaphores,
                    };

                    Context.Api.QueueSubmit(_queue, 1, in submitInfo, Fence);
                }
            }

            Context.Pool.DisposeCommandBuffer(this);
        }
    }
}
