using unsafe RenderPipelinePtr = Silk.NET.WebGPU.RenderPipeline*;

namespace GameDotNet.Graphics.WGPU.Wrappers;

public sealed class RenderPipeline : IDisposable
{
    private readonly WebGPU _api;
    private unsafe RenderPipelinePtr _handle;

    internal unsafe RenderPipelinePtr Handle 
    {
        get
        {
            if (_handle is null)
                throw new HandleDroppedOrDestroyedException(nameof(RenderPipeline));

            return _handle;
        }

        private set => _handle = value;
    }

    internal unsafe RenderPipeline(WebGPU api, RenderPipelinePtr handle)
    {
        if (handle is null)
            throw new ResourceCreationError(nameof(RenderPipeline));
        _api = api;

        Handle = handle;
    }

    public unsafe BindGroupLayout GetBindGroupLayout(uint groupIndex)
        => new(_api, _api.RenderPipelineGetBindGroupLayout(_handle, groupIndex));

    public unsafe void SetLabel(string label) => _api.RenderPipelineSetLabel(_handle, label);
        
    public unsafe void Dispose()
    {
        _api.RenderPipelineRelease(_handle);
        Handle = null;
    }
}