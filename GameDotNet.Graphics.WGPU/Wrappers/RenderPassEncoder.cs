using Silk.NET.WebGPU;
using unsafe RenderPassEncoderPtr = Silk.NET.WebGPU.RenderPassEncoder*;

namespace GameDotNet.Graphics.WGPU.Wrappers;

public sealed class RenderPassEncoder : IDisposable
{
    private readonly WebGPU _api;
    private unsafe RenderPassEncoderPtr _handle;

    internal unsafe RenderPassEncoder(WebGPU api, RenderPassEncoderPtr handle)
    {
        if (handle is null)
            throw new ResourceCreationError(nameof(RenderPassEncoder));

        _api = api;
        _handle = handle;
    }

    public unsafe void BeginOcclusionQuery(uint queryIndex)
        => _api.RenderPassEncoderBeginOcclusionQuery(_handle, queryIndex);

    public unsafe void BeginPipelineStatisticsQuery(QuerySet querySet, uint queryIndex)
        => _api.RenderPassEncoderBeginPipelineStatisticsQuery(_handle, querySet.Handle, queryIndex);

    public unsafe void Draw(uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance)
        => _api.RenderPassEncoderDraw(_handle, vertexCount, instanceCount, firstVertex, firstInstance);

    public unsafe void DrawIndexed(uint indexCount, uint instanceCount, uint firstIndex, int baseVertex,
                                   uint firstInstance)
        => _api.RenderPassEncoderDrawIndexed(_handle, indexCount, instanceCount, firstIndex, baseVertex, firstInstance);

    public unsafe void DrawIndexedIndirect(Buffer indirectBuffer, ulong indirectOffset)
        => _api.RenderPassEncoderDrawIndexedIndirect(_handle, indirectBuffer.Handle, indirectOffset);

    public unsafe void DrawIndirect(Buffer indirectBuffer, ulong indirectOffset)
        => _api.RenderPassEncoderDrawIndirect(_handle, indirectBuffer.Handle, indirectOffset);

    public unsafe void End() => _api.RenderPassEncoderEnd(_handle);

    public unsafe void EndOcclusionQuery() => _api.RenderPassEncoderEndOcclusionQuery(_handle);

    public unsafe void EndPipelineStatisticsQuery() => _api.RenderPassEncoderEndPipelineStatisticsQuery(_handle);

    public unsafe void ExecuteBundles(RenderBundle[] bundles)
    {
        Span<nint> innerBundles = stackalloc nint[bundles.Length];

        for (var i = 0; i < bundles.Length; i++)
            innerBundles[i] = (nint)bundles[i].Handle;

        fixed (nint* ptr = innerBundles)
            _api.RenderPassEncoderExecuteBundles(_handle, (uint)bundles.Length, (Silk.NET.WebGPU.RenderBundle**)ptr);
    }

    public unsafe void InsertDebugMarker(string markerLabel)
        => _api.RenderPassEncoderInsertDebugMarker(_handle, markerLabel);

    public unsafe void PushDebugGroup(string groupLabel)
        => _api.RenderPassEncoderPushDebugGroup(_handle, groupLabel);

    public unsafe void PopDebugGroup() => _api.RenderPassEncoderPopDebugGroup(_handle);

    public unsafe void SetBindGroup(uint groupIndex, BindGroup group, uint[]? dynamicOffsets = null)
    {
        using var dynamicOffsetsPtr = dynamicOffsets.AsMemory().Pin();
        _api.RenderPassEncoderSetBindGroup(_handle, groupIndex,
                                           group.Handle,
                                           (nuint)(dynamicOffsets?.Length ?? 0),
                                           (uint*)dynamicOffsetsPtr.Pointer);
    }

    public unsafe void SetBlendConstant(in Color color) => _api.RenderPassEncoderSetBlendConstant(_handle, color);

    public unsafe void SetIndexBuffer(Buffer buffer, IndexFormat format, ulong offset, ulong size)
        => _api.RenderPassEncoderSetIndexBuffer(_handle, buffer.Handle, format, offset, size);

    public unsafe void SetPipeline(RenderPipeline pipeline) =>
        _api.RenderPassEncoderSetPipeline(_handle, pipeline.Handle);

    // TODO: research why binding not present
    // public unsafe void SetPushConstants<T>(ShaderStage stages, uint offset, ReadOnlySpan<T> data)
    //     where T : unmanaged
    // {
    //     _api.RenderPassEncoderSetPushConstants(
    //                                       _handle, (uint)stages, offset, (uint)(data.Length * sizeof(T)),
    //                                       (nint)Unsafe.AsPointer(ref MemoryMarshal.GetReference(data))
    //                                      );
    // }

    public unsafe void SetScissorRect(uint x, uint y, uint width, uint height)
        => _api.RenderPassEncoderSetScissorRect(_handle, x, y, width, height);

    public unsafe void SetStencilReference(uint reference) =>
        _api.RenderPassEncoderSetStencilReference(_handle, reference);

    public unsafe void SetVertexBuffer(uint slot, Buffer buffer, ulong offset, ulong size)
        => _api.RenderPassEncoderSetVertexBuffer(_handle, slot, buffer.Handle, offset, size);

    public unsafe void SetViewport(float x, float y, float width, float height, float minDepth, float maxDepth)
        => _api.RenderPassEncoderSetViewport(_handle, x, y, width, height, minDepth, maxDepth);

    public unsafe void Dispose()
    {
        _api.RenderPassEncoderRelease(_handle);
        _handle = null;
    }
}