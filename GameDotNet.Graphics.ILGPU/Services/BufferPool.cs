using ILGPU.Runtime;

namespace GameDotNet.Graphics.ILGPU.Services;

/// <summary>
/// A zero-allocation struct that wraps a rented MemoryBuffer.
/// Disposing it returns the buffer to the pool.
/// </summary>
public readonly struct PooledBuffer : IDisposable
{
    private readonly BufferPool _pool;
    public MemoryBuffer Buffer { get; }

    internal PooledBuffer(BufferPool pool, MemoryBuffer buffer)
    {
        _pool = pool;
        Buffer = buffer;
    }

    public void Dispose()
    {
        _pool?.Return(Buffer);
    }
}

/// <summary>
/// Maintains a list of free buffers.
/// Simple for now, only allocates never shrinks.
/// TODO: improve to be able to split allocations and shrink pool when required. Explore partitioning this into buckets (e.g., 1MB, 4MB, 16MB)
/// </summary>
public sealed class BufferPool(Accelerator accelerator) : IDisposable
{
    private readonly HashSet<MemoryBuffer> _availableBuffers = [];

    public PooledBuffer Rent(long byteSize)
    {
        // Find the smallest buffer that satisfies the requested size (Best-Fit)
        var bestFit = _availableBuffers
            .Where(b => b.LengthInBytes >= byteSize)
            .OrderBy(b => b.LengthInBytes)
            .FirstOrDefault();

        if (bestFit != null)
        {
            _availableBuffers.Remove(bestFit);
            return new(this, bestFit);
        }

        // Fallback: Allocate new raw memory from the GPU
        var newBuffer = accelerator.AllocateRaw(byteSize, 1);
        return new(this, newBuffer);
    }

    internal void Return(MemoryBuffer buffer)
    {
        // Add back to the available pool
        _availableBuffers.Add(buffer);
    }

    public void Dispose()
    {
        // Actually destroy the physical VRAM allocations
        foreach (var buffer in _availableBuffers)
        {
            buffer.Dispose();
        }

        _availableBuffers.Clear();
    }
}
