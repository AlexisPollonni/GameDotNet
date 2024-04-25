using GameDotNet.Core.Tools.Extensions;
using Silk.NET.WebGPU;
using unsafe RenderBundleEncoderPtr = Silk.NET.WebGPU.RenderBundleEncoder*;

namespace GameDotNet.Graphics.WGPU.Wrappers;

public sealed class RenderBundleEncoder : IDisposable
{
    private readonly WebGPU _api;
    private unsafe RenderBundleEncoderPtr _handle;

    internal unsafe RenderBundleEncoder(WebGPU api, RenderBundleEncoderPtr handle)
    {
        if (handle is null)
            throw new ResourceCreationError(nameof(RenderBundleEncoder));

        _api = api;
        _handle = handle;
    }

    public unsafe void Draw(uint vertexCount, uint instanceCount, uint firstVertex, uint firstInstance)
        => _api.RenderBundleEncoderDraw(_handle, vertexCount, instanceCount, firstVertex, firstInstance);

    public unsafe void DrawIndexed(uint indexCount, uint instanceCount, uint firstIndex, int baseVertex,
                                   uint firstInstance)
        => _api.RenderBundleEncoderDrawIndexed(_handle, indexCount, instanceCount, firstIndex, baseVertex,
                                               firstInstance);

    public unsafe void DrawIndexedIndirect(Buffer indirectBuffer, ulong indirectOffset)
        => _api.RenderBundleEncoderDrawIndexedIndirect(_handle, indirectBuffer.Handle, indirectOffset);

    public unsafe void DrawIndirect(Buffer indirectBuffer, ulong indirectOffset)
        => _api.RenderBundleEncoderDrawIndirect(_handle, indirectBuffer.Handle, indirectOffset);

    public unsafe RenderBundle Finish(string label)
    {
        using var mem = label.ToGlobalMemory();
        return new(_api, _api.RenderBundleEncoderFinish(_handle, new RenderBundleDescriptor
                   {
                       Label = mem.AsPtr<byte>()
                   })
                  );
    }

    public unsafe void InsertDebugMarker(string markerLabel)
        => _api.RenderBundleEncoderInsertDebugMarker(_handle, markerLabel);

    public unsafe void PushDebugGroup(string groupLabel)
        => _api.RenderBundleEncoderPushDebugGroup(_handle, groupLabel);

    public unsafe void PopDebugGroup() => _api.RenderBundleEncoderPopDebugGroup(_handle);

    public unsafe void SetBindGroup(uint groupIndex, BindGroup group, uint[] dynamicOffsets)
    {
        fixed (uint* dynamicOffsetsPtr = dynamicOffsets)
        {
            _api.RenderBundleEncoderSetBindGroup(_handle, groupIndex,
                                                 group.Handle,
                                                 (uint)dynamicOffsets.Length,
                                                 dynamicOffsetsPtr
                                                );
        }
    }

    public unsafe void SetIndexBuffer(Buffer buffer, IndexFormat format, ulong offset, ulong size)
        => _api.RenderBundleEncoderSetIndexBuffer(_handle, buffer.Handle, format, offset, size);

    public unsafe void SetPipeline(RenderPipeline pipeline) =>
        _api.RenderBundleEncoderSetPipeline(_handle, pipeline.Handle);

    public unsafe void SetVertexBuffer(uint slot, Buffer buffer, ulong offset, ulong size)
        => _api.RenderBundleEncoderSetVertexBuffer(_handle, slot, buffer.Handle, offset, size);

    public unsafe void Dispose()
    {
        _api.RenderBundleEncoderRelease(_handle);
        _handle = null;
    }
}