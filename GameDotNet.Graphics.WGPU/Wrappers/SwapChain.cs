using System.Drawing;
using GameDotNet.Core.Tools.Extensions;
using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Extensions.Dawn;

namespace GameDotNet.Graphics.WGPU.Wrappers;

public sealed class SwapChain : IDisposable
{
    private readonly WebGPU _api;
    private readonly Dawn _dawn;

    internal unsafe Silk.NET.WebGPU.Extensions.Dawn.SwapChain* Handle { get; private set; }

    public unsafe SwapChain(WebGPU api, Dawn dawn, Silk.NET.WebGPU.Extensions.Dawn.SwapChain* handle)
    {
        _api = api;
        _dawn = dawn;
        Handle = handle;
    }

    public unsafe void Present()
    {
        _dawn.SwapChainPresent(Handle);
    }

    public unsafe Texture GetCurrentTexture()
    {
        var ptr = _dawn.SwapChainGetCurrentTexture(Handle);
        return new (_api, ptr);
    }

    public unsafe TextureView? GetCurrentTextureView()
    {
        var ptr = _dawn.SwapChainGetCurrentTextureView(Handle);

        if (ptr is null)
        {
            return null;
        }
        return new(_api, ptr);
    }

    public unsafe void Configure(Device device, Surface surface, Size size, TextureFormat fmt, TextureUsage usage,
                                 PresentMode presentMode, string? label = null)
    {
        _dawn.SwapChainRelease(Handle);
        
        var mem = label?.ToGlobalMemory();
        
        var swDesc = new SwapChainDescriptor
        {
            Width = (uint)size.Width,
            Height = (uint)size.Height,
            Format = fmt,
            Usage = usage,
            PresentMode = presentMode,
            Label = mem is null ? null : mem.AsPtr<byte>()
        };
        var swPtr = _dawn.DeviceCreateSwapChain(device.Handle, surface.Handle, &swDesc);

        if (swPtr is null) throw new ResourceCreationError("swapchain");

        Handle = swPtr;
    }
    
    public unsafe void Dispose()
    {
        _dawn.SwapChainRelease(Handle);
    }
}