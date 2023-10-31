using Silk.NET.WebGPU;
using unsafe CommandBufferPtr = Silk.NET.WebGPU.CommandBuffer*;

namespace GameDotNet.Graphics.WGPU.Wrappers;

public sealed class CommandBuffer : IDisposable
{
    private readonly WebGPU _api;
    private unsafe CommandBufferPtr _handle;

    internal unsafe CommandBufferPtr Handle 
    {
        get
        {
            if (_handle is null)
                throw new HandleDroppedOrDestroyedException(nameof(CommandBuffer));

            return _handle;
        }

        private set => _handle = value;
    }

    internal unsafe CommandBuffer(WebGPU api, CommandBufferPtr handle)
    {
        if (handle is null)
            throw new ResourceCreationError(nameof(CommandBuffer));
        _api = api;

        Handle = handle;
    }
        
    public unsafe void Dispose()
    {
        _api.CommandBufferRelease(_handle);
        _handle = null;
    }
}