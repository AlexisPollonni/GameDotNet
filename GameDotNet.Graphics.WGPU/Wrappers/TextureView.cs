using System.Collections.Concurrent;

namespace GameDotNet.Graphics.WGPU.Wrappers;

using unsafe TextureViewPtr = Silk.NET.WebGPU.TextureView*;

public sealed class TextureView : IDisposable
{
    private readonly WebGPU _api;
    private static readonly ConcurrentDictionary<nint, TextureView> Instances = new();

    private unsafe TextureViewPtr _handle;

    /// <summary>
    /// The Texture this TextureView belongs to. If this TextureView belongs to the SwapChain, then this is null.
    /// </summary>
    public Texture? Texture
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
            Instances.TryAdd((nint)handle, this);
    }

    /// <summary>
    /// To create a texture view from a swapchain
    /// </summary>
    /// <param name="api"></param>
    /// <param name="handle"></param>
    /// <exception cref="ResourceCreationError"></exception>
    internal unsafe TextureView(WebGPU api, TextureViewPtr handle)
    {
        if (handle is null)
            throw new ResourceCreationError(nameof(TextureView));
        _api = api;

        Handle = handle;
    }
    

    internal static unsafe void Forget(TextureView view) => Instances.TryRemove((nint)view._handle, out _);
        
    /// <summary>
    /// This function will be called automatically when this TextureView's associated Texture is disposed.
    /// If you dispose the TextureView yourself, 
    /// </summary>
    public unsafe void Dispose()
    {
        Texture?.RemoveTextureView(this);
        Forget(this);
        _api.TextureViewRelease(Handle);
        _handle = null;
    }
}