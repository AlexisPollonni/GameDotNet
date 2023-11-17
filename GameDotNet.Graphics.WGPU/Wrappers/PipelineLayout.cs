using Silk.NET.WebGPU;
using unsafe PipelineLayoutPtr = Silk.NET.WebGPU.PipelineLayout*;

namespace GameDotNet.Graphics.WGPU.Wrappers;

public sealed class PipelineLayout : IDisposable
{
    private readonly WebGPU _api;
    private unsafe PipelineLayoutPtr _handle;

    internal unsafe PipelineLayoutPtr Handle 
    {
        get
        {
            if (_handle is null)
                throw new HandleDroppedOrDestroyedException(nameof(PipelineLayout));

            return _handle;
        }

        private set => _handle = value;
    }

    internal unsafe PipelineLayout(WebGPU api, PipelineLayoutPtr handle)
    {
        if (handle is null)
            throw new ResourceCreationError(nameof(PipelineLayout));

        _api = api;
        _handle = handle;
    }
        
    public unsafe void Dispose()
    {
        _api.PipelineLayoutRelease(_handle);
        _handle = null;
    }
}