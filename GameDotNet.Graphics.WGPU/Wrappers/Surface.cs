using System.Drawing;
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

    public unsafe SurfaceTexture GetCurrentTexture()
    {
        var surfaceTexture = new Silk.NET.WebGPU.SurfaceTexture();
        _api.SurfaceGetCurrentTexture(Handle, ref surfaceTexture);

        var texture = surfaceTexture.Texture is null ? null : new Texture(_api, surfaceTexture.Texture);
        return new(surfaceTexture.Status, surfaceTexture.Suboptimal, texture);
    }

    public unsafe void Present()
    {
        _api.SurfacePresent(Handle);
    }

    public unsafe void Configure(Device device, SurfaceConfig config)
    {
        fixed(TextureFormat* frmts = config.ViewFormats)
        {
            var c = new SurfaceConfiguration
            {
                Device = device.Handle,
                Usage = config.Usage,
                Format = config.Format,
                PresentMode = config.PresentMode,
                AlphaMode = config.AlphaMode,
                Width = (uint)config.Size.Width,
                Height = (uint)config.Size.Height,
                ViewFormatCount = (nuint)(config.ViewFormats?.Length ?? 0),
                ViewFormats = frmts
            };
            _api.SurfaceConfigure(Handle, &c);
        }
    }

    public unsafe void Unconfigure(Device device)
    {
        _api.SurfaceUnconfigure(Handle);
    }

    public unsafe SurfaceCapabilities GetCapabilities(Adapter adapter)
    {
        var nat = new Silk.NET.WebGPU.SurfaceCapabilities();
        _api.SurfaceGetCapabilities(Handle, adapter.Handle, ref nat);

        var frmts = new ReadOnlySpan<TextureFormat>(nat.Formats, (int)nat.FormatCount);
        var pres = new ReadOnlySpan<PresentMode>(nat.PresentModes, (int)nat.PresentModeCount);
        var alpha = new ReadOnlySpan<CompositeAlphaMode>(nat.AlphaModes, (int)nat.AlphaModeCount);

        var managed = new SurfaceCapabilities(frmts.ToArray(), pres.ToArray(), alpha.ToArray());
            
        _api.SurfaceCapabilitiesFreeMembers(nat);

        return managed;
    }

    public unsafe void Dispose()
    {
        _api.SurfaceRelease(Handle);
        Handle = default;
    }
}

public readonly record struct SurfaceConfig(
    TextureFormat Format,
    TextureUsage Usage,
    TextureFormat[]? ViewFormats,
    CompositeAlphaMode AlphaMode,
    Size Size,
    PresentMode PresentMode);


public readonly record struct SurfaceCapabilities(
    TextureFormat[] Formats,
    PresentMode[] PresentModes,
    CompositeAlphaMode[] AlphaModes
    );

public readonly record struct SurfaceTexture(SurfaceGetCurrentTextureStatus Status, bool Suboptimal, Texture? Texture);