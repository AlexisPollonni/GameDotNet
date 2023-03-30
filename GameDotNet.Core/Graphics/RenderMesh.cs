using GameDotNet.Core.Graphics.MemoryAllocation;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace GameDotNet.Core.Graphics;

public record struct RenderMesh(Mesh Mesh)
{
    public Mesh Mesh = Mesh;
    public DisposableBuffer? RenderBuffer { get; set; }
}

public class DisposableBuffer : IDisposable
{
    public Buffer Buffer { get; }
    public Allocation Allocation { get; }

    public DisposableBuffer(Buffer buffer, Allocation allocation)
    {
        Buffer = buffer;
        Allocation = allocation;
    }

    public void Dispose()
    {
        Allocation.Dispose();

        GC.SuppressFinalize(this);
    }
}