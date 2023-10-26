using System.Collections.Concurrent;
using Silk.NET.WebGPU;

namespace GameDotNet.Graphics.WGPU.Wrappers;

using unsafe TextureViewPtr = Silk.NET.WebGPU.TextureView*;

public sealed class TextureView : IDisposable
{
    private readonly WebGPU _api;
    private static readonly ConcurrentDictionary<nint, TextureView> _instances = new();

    private unsafe TextureViewPtr _handle;

    /// <summary>
    /// The Texture this TextureView belongs to. If this TextureView belongs to the SwapChain, then this is null.
    /// </summary>
    public Texture Texture
    {
        get;
    }

    internal unsafe TextureViewPtr Handle 
    {
        get
        {
            if (_handle is null)
                throw new HandleDroppedOrDestroyedException(nameof(TextureView));

            return _handle;
        }

        set => _handle = value;
    }

    internal unsafe TextureView(WebGPU api, TextureViewPtr handle, Texture texture, bool tracked = true)
    {
        if (handle is null)
            throw new ResourceCreationError(nameof(TextureView));
        _api = api;

        Handle = handle;
        Texture = texture;

        if(tracked)
            _instances.TryAdd((nint)handle, this);
    }

    internal static unsafe void Forget(TextureView view) => _instances.TryRemove((nint)view._handle, out _);
        
    /// <summary>
    /// This function will be called automatically when this TextureView's associated Texture is disposed.
    /// If you dispose the TextureView yourself, 
    /// </summary>
    public unsafe void Dispose()
    {
        Texture.RemoveTextureView(this);
        Forget(this);
        _api.TextureViewRelease(Handle);
        _handle = null;
    }
}