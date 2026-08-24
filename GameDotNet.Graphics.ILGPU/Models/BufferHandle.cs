using GameDotNet.Graphics.ILGPU.Services;
using ILGPU;

namespace GameDotNet.Graphics.ILGPU.Models;

public readonly record struct BufferHandle<T, TIndex, TStride>
    where T : unmanaged
    where TStride : struct, IStride<TIndex>
    where TIndex : struct, IGenericIndex<TIndex>
{
    internal BufferHandle(LogicalResource Resource) => this.Resource = Resource;

    internal LogicalResource Resource { get; init; }

    internal void Deconstruct(out LogicalResource resource)
    {
        resource = Resource;
    }
}
