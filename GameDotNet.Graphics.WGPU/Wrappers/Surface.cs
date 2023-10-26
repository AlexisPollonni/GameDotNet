using Silk.NET.WebGPU;

namespace GameDotNet.Graphics.WGPU.Wrappers;

public sealed class Surface : IDisposable
{
    private readonly WebGPU _api;
    internal unsafe Silk.NET.WebGPU.Surface* Handle { get; private set; }

    internal unsafe Surface(WebGPU api, Silk.NET.WebGPU.Surface* handle)
    {
        if (handle is null)
            throw new ResourceCreationError(nameof(Surface));
        _api = api;

        Handle = handle;
    }

    public unsafe TextureFormat GetPreferredFormat(Adapter adapter)
    {
        return _api.SurfaceGetPreferredFormat(Handle, adapter.Handle);
    }
        
    public unsafe Texture GetCurrentTexture()
    {
        var surfaceTexture = new SurfaceTexture();
        _api.SurfaceGetCurrentTexture(Handle, ref surfaceTexture);
            
        //TODO: maybe take status into account later
        return new(_api, surfaceTexture.Texture);
    }

    public unsafe void Present()
    {
        _api.SurfacePresent(Handle);
    }

    public unsafe void Dispose()
    {
        _api.SurfaceRelease(Handle);
        Handle = default;
    }
}