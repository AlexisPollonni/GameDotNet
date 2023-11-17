using Silk.NET.WebGPU;
using unsafe SamplerPtr = Silk.NET.WebGPU.Sampler*;

namespace GameDotNet.Graphics.WGPU.Wrappers;

public sealed class Sampler : IDisposable
{
    private readonly WebGPU _api;
    private unsafe SamplerPtr _handle;

    internal unsafe SamplerPtr Handle 
    {
        get
        {
            if (_handle is null)
                throw new HandleDroppedOrDestroyedException(nameof(Sampler));

            return _handle;
        }

        private set => _handle = value;
    }

    internal unsafe Sampler(WebGPU api, SamplerPtr handle)
    {
        if (handle is null)
            throw new ResourceCreationError(nameof(Sampler));
        _api = api;

        Handle = handle;
    }
        
    public unsafe void Dispose()
    {
        _api.SamplerRelease(_handle);
        _handle = null;
    }
}