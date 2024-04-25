using Silk.NET.WebGPU;
using unsafe ComputePipelinePtr = Silk.NET.WebGPU.ComputePipeline*;

namespace GameDotNet.Graphics.WGPU.Wrappers;

public sealed class ComputePipeline : IDisposable
{
    private readonly WebGPU _api;
    private unsafe ComputePipelinePtr _handle;

    internal unsafe ComputePipelinePtr Handle
    {
        get
        {
            if (_handle is null)
                throw new HandleDroppedOrDestroyedException(nameof(ComputePipeline));

            return _handle;
        }

        private set => _handle = value;
    }

    internal unsafe ComputePipeline(WebGPU api, ComputePipelinePtr handle)
    {
        if (handle is null)
            throw new ResourceCreationError(nameof(ComputePipeline));

        _api = api;
        _handle = handle;
    }

    public unsafe BindGroupLayout GetBindGroupLayout(uint groupIndex)
        => new(_api, _api.ComputePipelineGetBindGroupLayout(_handle, groupIndex));

    public unsafe void SetLabel(string label) => _api.ComputePipelineSetLabel(_handle, label);
        
    public unsafe void Dispose()
    {
        _api.ComputePipelineRelease(_handle);
        _handle = null;
    }
}