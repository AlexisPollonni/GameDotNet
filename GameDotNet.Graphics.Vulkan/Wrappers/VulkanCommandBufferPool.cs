using CommunityToolkit.HighPerformance;
using GameDotNet.Graphics.Vulkan.Tools.Allocators;
using GameDotNet.Graphics.Vulkan.Tools.Extensions;
using Silk.NET.Vulkan;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace GameDotNet.Graphics.Vulkan.Wrappers;

public sealed class VulkanCommandBufferPool : IDisposable
{
    private readonly Vk _api;
    private readonly VulkanDevice _device;
    private readonly DeviceQueue _queue;
    private readonly IVulkanAllocCallback _callbacks;
    private readonly CommandPool _commandPool;

    private readonly List<CommandBuffer> _usedCommandBuffers = new();
    private readonly object _lock = new();


    public VulkanCommandBufferPool(Vk api, VulkanDevice device, DeviceQueue queue, IVulkanAllocCallback callbacks)
    {
        _api = api;
        _device = device;
        _queue = queue;
        _callbacks = callbacks;

        var commandPoolCreateInfo = new CommandPoolCreateInfo
        {
            SType = StructureType.CommandPoolCreateInfo,
            Flags = CommandPoolCreateFlags.TransientBit,
            QueueFamilyIndex = (uint)queue.FamilyIndex
        };

        _api.CreateCommandPool(_device, commandPoolCreateInfo, callbacks.Handle,
                               out _commandPool)
            .ThrowOnError();
    }

    public void Dispose()
    {
        lock (_lock)
        {
            FreeUsedCommandBuffers();
            _api.DestroyCommandPool(_device, _commandPool, _callbacks.Handle);
        }
    }

    public VulkanCommandBuffer CreateCommandBuffer(VulkanFence? fence = null)
    {
        return new(_api, _device, _queue, this, fence);
    }

    public void FreeUsedCommandBuffers()
    {
        lock (_lock)
        {
            var s = _usedCommandBuffers.AsSpan();
            _api.FreeCommandBuffers(_device, _commandPool, (uint)s.Length, s);

            _usedCommandBuffers.Clear();
        }
    }

    private CommandBuffer AllocateCommandBuffer()
    {
        var commandBufferAllocateInfo = new CommandBufferAllocateInfo
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = _commandPool,
            CommandBufferCount = 1,
            Level = CommandBufferLevel.Primary
        };

        lock (_lock)
        {
            _api.AllocateCommandBuffers(_device, commandBufferAllocateInfo, out var commandBuffer);

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

    public sealed class VulkanCommandBuffer : IDisposable
    {
        public VulkanFence Fence { get; }

        private readonly VulkanCommandBufferPool _commandBufferPool;
        private readonly Vk _api;
        private readonly VulkanDevice _device;
        private readonly DeviceQueue _queue;
        private readonly bool _fenceExternal;

        private bool _hasEnded;
        private bool _hasStarted;

        public nint Handle => InternalHandle.Handle;

        internal CommandBuffer InternalHandle { get; }

        internal VulkanCommandBuffer(Vk api, VulkanDevice device, DeviceQueue queue,
                                     VulkanCommandBufferPool commandBufferPool, VulkanFence? fence)
        {
            _api = api;
            _device = device;
            _queue = queue;
            _commandBufferPool = commandBufferPool;
            _fenceExternal = fence is not null;

            Fence = fence ?? new(api, device, FenceCreateFlags.SignaledBit,
                                 callbacks: commandBufferPool._callbacks.WithUserData("CmdBufferPool::Fence"));

            InternalHandle = _commandBufferPool.AllocateCommandBuffer();
        }

        public static implicit operator CommandBuffer(VulkanCommandBuffer buffer) => buffer.InternalHandle;

        public void Dispose()
        {
            Fence.Wait();
            lock (_commandBufferPool._lock)
            {
                _api.FreeCommandBuffers(_device, _commandBufferPool._commandPool, 1, InternalHandle);
            }

            if (!_fenceExternal)
                Fence.Dispose();
        }

        public void BeginRecording()
        {
            if (_hasStarted) return;

            Fence.Wait();
            Fence.Reset();

            var beginInfo = new CommandBufferBeginInfo
            {
                SType = StructureType.CommandBufferBeginInfo,
                Flags = CommandBufferUsageFlags.OneTimeSubmitBit
            };

            _api.BeginCommandBuffer(InternalHandle, beginInfo);

            _hasStarted = true;
        }

        public void EndRecording()
        {
            if (!_hasStarted || _hasEnded) return;

            _hasEnded = true;

            _api.EndCommandBuffer(InternalHandle);
        }


        public void Submit(VulkanSemaphore? wait = null, PipelineStageFlags? waitDstStageMask = null,
                           VulkanSemaphore? signal = null)
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
            ReadOnlySpan<Semaphore> signalSemaphores)
        {
            EndRecording();

            fixed (Semaphore* pWaitSemaphores = waitSemaphores, pSignalSemaphores = signalSemaphores)
            {
                fixed (PipelineStageFlags* pWaitDstStageMask = waitDstStageMask)
                {
                    var commandBuffer = InternalHandle;
                    var submitInfo = new SubmitInfo
                    {
                        SType = StructureType.SubmitInfo,
                        WaitSemaphoreCount = !waitSemaphores.IsEmpty ? (uint)waitSemaphores.Length : 0,
                        PWaitSemaphores = pWaitSemaphores,
                        PWaitDstStageMask = pWaitDstStageMask,
                        CommandBufferCount = 1,
                        PCommandBuffers = &commandBuffer,
                        SignalSemaphoreCount = !signalSemaphores.IsEmpty ? (uint)signalSemaphores.Length : 0,
                        PSignalSemaphores = pSignalSemaphores,
                    };

                    _api.QueueSubmit(_queue, 1, submitInfo, Fence);
                }
            }

            _commandBufferPool.DisposeCommandBuffer(this);
        }
    }
}