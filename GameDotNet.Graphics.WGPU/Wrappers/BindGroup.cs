using unsafe BindGroupPtr = Silk.NET.WebGPU.BindGroup*;

namespace GameDotNet.Graphics.WGPU.Wrappers;

public class BindGroup : IDisposable
{
    private readonly WebGPU _api;
    private unsafe BindGroupPtr _handle;

    internal unsafe BindGroupPtr Handle
    {
        get
        {
            if (_handle is null)
                throw new HandleDroppedOrDestroyedException(nameof(BindGroup));

            return _handle;
        }

        private set => _handle = value;
    }

    internal unsafe BindGroup(WebGPU api, BindGroupPtr handle)
    {
        if (handle is null)
            throw new ResourceCreationError(nameof(BindGroup));

        _api = api;
        Handle = handle;
    }

    public unsafe void Dispose()
    {
        _api.BindGroupRelease(_handle);
        _handle = null;
    }
}