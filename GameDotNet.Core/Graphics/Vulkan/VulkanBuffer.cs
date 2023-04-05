using GameDotNet.Core.Graphics.MemoryAllocation;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace GameDotNet.Core.Graphics.Vulkan;

public class VulkanBuffer : IDisposable
{
    public Buffer Buffer { get; }
    public Allocation Allocation { get; }


    private readonly VulkanMemoryAllocator _allocator;

    public VulkanBuffer(VulkanMemoryAllocator allocator, in BufferCreateInfo buffInfo,
                        in AllocationCreateInfo allocInfo)
    {
        _allocator = allocator;

        Buffer = _allocator.CreateBuffer(buffInfo, allocInfo, out var alloc);
        Allocation = alloc;
    }

    public VulkanBuffer(VulkanMemoryAllocator allocator, Buffer buffer, Allocation allocation)
    {
        _allocator = allocator;
        Buffer = buffer;
        Allocation = allocation;
    }

    public static implicit operator Buffer(VulkanBuffer buff) => buff.Buffer;

    public BufferDisposableMapping<T> Map<T>() where T : unmanaged => new(_allocator, Allocation);

    public void Dispose()
    {
        Allocation.Dispose();

        GC.SuppressFinalize(this);
    }


    public sealed class BufferDisposableMapping<T> : IDisposable where T : unmanaged
    {
        public bool IsMapped => _allocation.MappedData != IntPtr.Zero;

        private readonly VulkanMemoryAllocator _allocator;
        private readonly Allocation _allocation;

        internal BufferDisposableMapping(VulkanMemoryAllocator allocator, Allocation allocation)
        {
            _allocator = allocator;
            _allocation = allocation;

            allocation.Map();
        }

        public bool TryGetSpan(out Span<T> span)
        {
            if (!IsMapped) throw new InvalidOperationException("Can't access buffer memory without it being mapped");

            return _allocation.TryGetSpan(out span);
        }

        public bool TryGetMemory(out Memory<T> memory)
        {
            if (!IsMapped) throw new InvalidOperationException("Can't access buffer memory without it being mapped");

            return _allocation.TryGetMemory(out memory);
        }

        public void Dispose()
        {
            _allocation.Unmap();
        }
    }
}