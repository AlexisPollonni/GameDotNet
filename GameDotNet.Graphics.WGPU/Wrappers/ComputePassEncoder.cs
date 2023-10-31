using Silk.NET.WebGPU;
using unsafe ComputePassEncoderPtr = Silk.NET.WebGPU.ComputePassEncoder*;

namespace GameDotNet.Graphics.WGPU.Wrappers;

public sealed class ComputePassEncoder : IDisposable
{
    private readonly WebGPU _api;
    internal unsafe ComputePassEncoderPtr Handle { get; private set; }

    internal unsafe ComputePassEncoder(WebGPU api, ComputePassEncoderPtr handle)
    {
        if (handle is null)
            throw new ResourceCreationError(nameof(ComputePassEncoder));
        _api = api;

        Handle = handle;
    }

    public unsafe void BeginPipelineStatisticsQuery(QuerySet querySet, uint queryIndex)
        => _api.ComputePassEncoderBeginPipelineStatisticsQuery(Handle, querySet.Handle, queryIndex);

    public unsafe void DispatchWorkgroups(uint workgroupCountX, uint workgroupCountY, uint workgroupCountZ)
        => _api.ComputePassEncoderDispatchWorkgroups(Handle, workgroupCountX, workgroupCountY, workgroupCountZ);

    public unsafe void DispatchWorkgroupsIndirect(Buffer indirectBuffer, ulong indirectOffset)
        => _api.ComputePassEncoderDispatchWorkgroupsIndirect(Handle, indirectBuffer.Handle, indirectOffset);

    public unsafe void End() => _api.ComputePassEncoderEnd(Handle);

    public unsafe void EndPipelineStatisticsQuery() => _api.ComputePassEncoderEndPipelineStatisticsQuery(Handle);

    public unsafe void InsertDebugMarker(string markerLabel)
        => _api.ComputePassEncoderInsertDebugMarker(Handle, markerLabel);

    public unsafe void PushDebugGroup(string groupLabel)
        => _api.ComputePassEncoderPushDebugGroup(Handle, groupLabel);

    public unsafe void PopDebugGroup(string groupLabel) => _api.ComputePassEncoderPopDebugGroup(Handle);

    public unsafe void SetBindGroup(uint groupIndex, BindGroup group, uint[] dynamicOffsets)
    {
        fixed (uint* dynamicOffsetsPtr = dynamicOffsets)
        {
            _api.ComputePassEncoderSetBindGroup(Handle, groupIndex,
                                                group.Handle,
                                                (uint)dynamicOffsets.Length,
                                                dynamicOffsetsPtr
                                               );
        }
    }

    public unsafe void SetPipeline(ComputePipeline pipeline) => _api.ComputePassEncoderSetPipeline(Handle, pipeline.Handle);

    public unsafe void Dispose()
    {
        _api.ComputePassEncoderRelease(Handle);
        Handle = null;
    }
}