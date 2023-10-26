using GameDotNet.Core.Tools.Containers;
using GameDotNet.Core.Tools.Extensions;
using Silk.NET.Core.Native;
using Silk.NET.WebGPU;

namespace GameDotNet.Graphics.WGPU.Wrappers;

public sealed class Instance : IDisposable
{
    private readonly WebGPU _api;
    private unsafe Silk.NET.WebGPU.Instance* _handle;

    public Instance(WebGPU api)
    {
        _api = api;
            
        unsafe
        {
            _handle = api.CreateInstance(new InstanceDescriptor());
        }
    }

    public unsafe Surface CreateSurfaceFromAndroidNativeWindow(void* window, string label = "")
    {
        using var d = new DisposableList();
        return new (_api, _api.InstanceCreateSurface(_handle, new SurfaceDescriptor
        {
            Label = label.ToPtr(d),
            NextInChain = new WgpuStructChain()
                          .AddSurfaceDescriptorFromAndroidNativeWindow(window)
                          .DisposeWith(d)
                          .Ptr
        }));
    }

    public unsafe Surface CreateSurfaceFromCanvasHTMLSelector(string selector, string label = "")
    {
        using var d = new DisposableList();
        return new (_api, _api.InstanceCreateSurface(_handle, new SurfaceDescriptor
        {
            Label = label.ToPtr(d),
            NextInChain = new WgpuStructChain()
                          .AddSurfaceDescriptorFromCanvasHTMLSelector(selector)
                          .DisposeWith(d)
                          .Ptr
        }));
    }

    public unsafe Surface CreateSurfaceFromMetalLayer(void* layer, string label = "")
    {
        using var d = new DisposableList();
        return new(_api, _api.InstanceCreateSurface(_handle, new SurfaceDescriptor
        {
            Label = label.ToPtr(d),
            NextInChain = new WgpuStructChain()
                          .AddSurfaceDescriptorFromMetalLayer(layer)
                          .DisposeWith(d)
                          .Ptr
        }));
    }

    public unsafe Surface CreateSurfaceFromWaylandSurface(void* display, string label = "")
    {
        using var d = new DisposableList();
        return new(_api, _api.InstanceCreateSurface(_handle, new SurfaceDescriptor
        {
            Label = label.ToPtr(d),
            NextInChain = new WgpuStructChain()
                          .AddSurfaceDescriptorFromWaylandSurface(display)
                          .DisposeWith(d)
                          .Ptr
        }));
    }

    public unsafe Surface CreateSurfaceFromWindowsHWND(void* hinstance, void* hwnd, string label = "")
    {
        using var d = new DisposableList();
        return new(_api, _api.InstanceCreateSurface(_handle, new SurfaceDescriptor
        {
            Label = label.ToPtr(d),
            NextInChain = new WgpuStructChain()
                          .AddSurfaceDescriptorFromWindowsHWND(hinstance, hwnd)
                          .DisposeWith(d)
                          .Ptr
        }));
    }

    public unsafe Surface CreateSurfaceFromXcbWindow(void* connection, uint window, string label = "")
    {
        using var d = new DisposableList();
        return new(_api, _api.InstanceCreateSurface(_handle, new SurfaceDescriptor
        {
            Label = label.ToPtr(d),
            NextInChain = new WgpuStructChain()
                          .AddSurfaceDescriptorFromXcbWindow(connection, window)
                          .DisposeWith(d)
                          .Ptr
        }));
    }

    public unsafe Surface CreateSurfaceFromXlibWindow(void* display, uint window, string label = "")
    {
        using var d = new DisposableList();
        return new(_api, _api.InstanceCreateSurface(_handle, new SurfaceDescriptor
        {
            Label = label.ToPtr(d),
            NextInChain = new WgpuStructChain()
                          .AddSurfaceDescriptorFromXlibWindow(display, window)
                          .DisposeWith(d)
                          .Ptr
        }));
    }

    public unsafe void ProcessEvents() => _api.InstanceProcessEvents(_handle);

    public unsafe void RequestAdapter(Surface compatibleSurface, PowerPreference powerPreference, bool forceFallbackAdapter, RequestAdapterCallback callback, BackendType backendType)
    {
        var cb = new PfnRequestAdapterCallback((s, a, m, _) 
                                                   =>
                                               {
                                                   callback(s, new(_api, a), SilkMarshal.PtrToString((nint)m)!);
                                               });
        var options = new RequestAdapterOptions
        {
            CompatibleSurface = compatibleSurface.Handle,
            PowerPreference = powerPreference,
            ForceFallbackAdapter = forceFallbackAdapter,
            BackendType = backendType
        };
        _api.InstanceRequestAdapter(_handle, options, cb, null);
    }

    public unsafe void Dispose()
    {
        _api.InstanceRelease(_handle);
        _handle = default;
    }
}

public delegate void RequestAdapterCallback(RequestAdapterStatus status, Adapter adapter, string message);