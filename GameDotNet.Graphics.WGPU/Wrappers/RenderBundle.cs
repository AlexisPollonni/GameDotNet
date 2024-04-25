using Silk.NET.WebGPU;
using unsafe RenderBundlePtr = Silk.NET.WebGPU.RenderBundle*;

namespace GameDotNet.Graphics.WGPU.Wrappers;

public sealed class RenderBundle : IDisposable
{
    private readonly WebGPU _api;
    private unsafe RenderBundlePtr _handle;

    internal unsafe RenderBundlePtr Handle 
    {
        get
        {
            if (_handle is null)
                throw new HandleDroppedOrDestroyedException(nameof(RenderBundle));

            return _handle;
        }

        private set => _handle = value;
    }

    internal unsafe RenderBundle(WebGPU api, RenderBundlePtr handle)
    {
        if (handle is null)
            throw new ResourceCreationError(nameof(RenderBundle));
        _api = api;
        Handle = handle;
    }
        
    public unsafe void Dispose()
    {
        _api.RenderBundleRelease(_handle);
        _handle = null;
    }
}