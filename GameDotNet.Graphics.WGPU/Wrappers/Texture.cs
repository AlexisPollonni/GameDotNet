using GameDotNet.Core.Tools.Containers;
using Silk.NET.Core.Native;
using unsafe TexturePtr = Silk.NET.WebGPU.Texture*;

namespace GameDotNet.Graphics.WGPU.Wrappers;

public sealed class Texture : IDisposable
{
    private readonly WebGPU _api;
    private unsafe TexturePtr _handle;
        
    private readonly HashSet<TextureView> _createdViews;

    public string? Label { get; }
    public TextureUsage Usage { get; private set; }
    public TextureDimension Dimension { get; private set; }
    public Extent3D Size { get; }
    public TextureFormat Format { get; }
    public uint MipLevelCount { get; }
    public uint SampleCount { get; private set; }

    internal unsafe TexturePtr Handle
    {
        get
        {
            if (_handle is null)
                throw new HandleDroppedOrDestroyedException(nameof(Texture));

            return _handle;
        }

        private set => _handle = value;
    }

    internal unsafe Texture(WebGPU api, TexturePtr handle, TextureDescriptor descriptor)
    {
        if(handle is null)
            throw new ResourceCreationError(nameof(Texture));
        _handle = handle;
        _api = api;

        _createdViews = new();

        Label = SilkMarshal.PtrToString((nint)descriptor.Label);
        Usage = descriptor.Usage;
        Dimension = descriptor.Dimension;
        Size = descriptor.Size;
        Format = descriptor.Format;
        MipLevelCount = descriptor.MipLevelCount;
        SampleCount = descriptor.SampleCount;
    }

    internal unsafe Texture(WebGPU api, TexturePtr handle)
    {
        if(handle is null)
            throw new ResourceCreationError(nameof(Texture));
        _handle = handle;
        _api = api;

        _createdViews = new();

        Usage = _api.TextureGetUsage(handle);
        Dimension = _api.TextureGetDimension(_handle);
        Size = new(_api.TextureGetWidth(_handle), _api.TextureGetHeight(_handle),
                   _api.TextureGetDepthOrArrayLayers(_handle));
        Format = _api.TextureGetFormat(_handle);
        MipLevelCount = _api.TextureGetMipLevelCount(_handle);
        SampleCount = _api.TextureGetSampleCount(_handle);
    }

    public unsafe TextureView CreateTextureView(string label, TextureFormat format, TextureViewDimension dimension,
                                                uint baseMipLevel, uint mipLevelCount, uint baseArrayLayer, uint arrayLayerCount,
                                                TextureAspect aspect)
    {
        using var d = new DisposableList();
        var view = new TextureView(_api, _api.TextureCreateView(Handle, new TextureViewDescriptor
        {
            Label = label.ToPtr(d),
            Format = format,
            Dimension = dimension,
            BaseMipLevel = baseMipLevel,
            MipLevelCount = mipLevelCount,
            BaseArrayLayer = baseArrayLayer,
            ArrayLayerCount = arrayLayerCount,
            Aspect = aspect
        }), this);

        _createdViews.Add(view);
        return view;
    }

    public unsafe TextureView CreateTextureView()
    {
        var view = new TextureView(_api, _api.TextureCreateView(Handle, null), this);
        
        _createdViews.Add(view);
        return view;
    }

    internal void RemoveTextureView(TextureView view)
    {
        if (view.Texture != this)
            throw new TextureDoesNotOwnViewException(Label);
                    
        _createdViews.Remove(view);
    }

    public unsafe void Dispose()
    {
        foreach (var view in _createdViews)
        {
            view.Dispose();
        }
        _createdViews.Clear();
            
        //_api.TextureDestroy(_handle);
        _api.TextureRelease(_handle);
        _handle = null;
    }
}

public static class TextureExtensions
{
    public static TextureView CreateTextureView(this Texture texture)
    {
        return texture.CreateTextureView(texture.Label + " View",
                                         texture.Format,
                                         texture.Dimension switch
                                         {
                                             TextureDimension.Dimension1D => TextureViewDimension.Dimension1D,
                                             TextureDimension.Dimension2D => TextureViewDimension.Dimension2D,
                                             TextureDimension.Dimension3D => TextureViewDimension.Dimension3D,
                                             TextureDimension.DimensionForce32 => TextureViewDimension.DimensionForce32,
                                             _ => throw new ArgumentException("Invalid value", nameof(texture.Dimension))
                                         },
                                         0,
                                         texture.MipLevelCount,
                                         0,
                                         texture.Size.DepthOrArrayLayers,
                                         TextureAspect.All);
    }
}