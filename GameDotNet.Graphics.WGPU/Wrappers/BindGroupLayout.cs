using Silk.NET.WebGPU;

namespace GameDotNet.Graphics.WGPU.Wrappers;

using unsafe BindGroupLayoutPtr = Silk.NET.WebGPU.BindGroupLayout*;

public sealed class BindGroupLayout : IDisposable
{
    private static Dictionary<nint, BindGroupLayout> _instances = new();

    private readonly WebGPU _api;
    private unsafe BindGroupLayoutPtr _handle;

    internal unsafe BindGroupLayoutPtr Handle 
    {
        get
        {
            if (_handle is null)
                throw new HandleDroppedOrDestroyedException(nameof(BindGroupLayout));

            return _handle;
        }

        private set => _handle = value;
    }

    internal unsafe BindGroupLayout(WebGPU api, BindGroupLayoutPtr handle)
    {
        if (handle is null)
            throw new ResourceCreationError(nameof(BindGroupLayout));

        _api = api;
        _handle = handle;
    }
    
    public unsafe void Dispose()
    {
        _api.BindGroupLayoutRelease(_handle);
        Handle = null;
    }
}