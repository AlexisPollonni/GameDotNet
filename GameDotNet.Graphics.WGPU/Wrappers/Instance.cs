using GameDotNet.Core.Tools.Containers;
using GameDotNet.Core.Tools.Extensions;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Extensions.Dawn;

namespace GameDotNet.Graphics.WGPU.Wrappers;

public unsafe struct DawnInstanceDescriptor
{
    public InstanceFeatures Features;
    public ChainedStruct* Next;
} 

public sealed class Instance : IDisposable
{
    private readonly WebGPU _api;
    private unsafe Silk.NET.WebGPU.Instance* _handle;

    public Instance(WebGPU api)
    {
        _api = api;

        unsafe
        {
            var desc = new DawnInstanceDescriptor
                { Features = new() { TimedWaitAnyEnable = false, TimedWaitAnyMaxCount = 0 } };
            _handle = api.CreateInstance((InstanceDescriptor*)(&desc));
        }
    }

    public unsafe Surface CreateSurfaceFromAndroidNativeWindow(void* window, string label = "")
        => CreateSurfaceFromCore(new WgpuStructChain()
                                     .AddSurfaceDescriptorFromAndroidNativeWindow(window), label);

    public unsafe Surface CreateSurfaceFromCanvasHTMLSelector(string selector, string label = "")
        => CreateSurfaceFromCore(new WgpuStructChain()
                                     .AddSurfaceDescriptorFromCanvasHTMLSelector(selector), label);

    public unsafe Surface CreateSurfaceFromMetalLayer(void* layer, string label = "")
        => CreateSurfaceFromCore(new WgpuStructChain()
                                     .AddSurfaceDescriptorFromMetalLayer(layer), label);

    public unsafe Surface CreateSurfaceFromWaylandSurface(void* display, string label = "")
        => CreateSurfaceFromCore(new WgpuStructChain()
                                     .AddSurfaceDescriptorFromWaylandSurface(display), label);

    public unsafe Surface CreateSurfaceFromWindowsHWND(void* hinstance, void* hwnd, string label = "")
        => CreateSurfaceFromCore(new WgpuStructChain()
                                     .AddSurfaceDescriptorFromWindowsHWND(hinstance, hwnd), label);

    public unsafe Surface CreateSurfaceFromXcbWindow(void* connection, uint window, string label = "")
        => CreateSurfaceFromCore(new WgpuStructChain()
                                     .AddSurfaceDescriptorFromXcbWindow(connection, window), label);

    public unsafe Surface CreateSurfaceFromXlibWindow(void* display, uint window, string label = "") =>
        CreateSurfaceFromCore(new WgpuStructChain()
                                  .AddSurfaceDescriptorFromXlibWindow(display, window), label);

    private unsafe Surface CreateSurfaceFromCore(WgpuStructChain next, string label)
    {
        using var d = new DisposableList();
        return new(_api, _api.InstanceCreateSurface(_handle, new SurfaceDescriptor
        {
            Label = label.ToPtr(d),
            NextInChain = next.DisposeWith(d).Ptr
        }));
    }

    public unsafe void ProcessEvents() => _api.InstanceProcessEvents(_handle);

    public unsafe void RequestAdapter(Surface compatibleSurface, PowerPreference powerPreference,
                                      bool forceFallbackAdapter, RequestAdapterCallback callback,
                                      BackendType backendType)
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

    public Task<Adapter> RequestAdapterAsync(Surface compatibleSurface, PowerPreference powerPreference,
                                             bool forceFallbackAdapter, BackendType backendType,
                                             CancellationToken token = default)
    {
        var tcs = new TaskCompletionSource<Adapter>();

        token.ThrowIfCancellationRequested();
        RequestAdapter(compatibleSurface, powerPreference, forceFallbackAdapter, (status, adapter, message) =>
        {
            token.ThrowIfCancellationRequested();
            if (status is not RequestAdapterStatus.Success)
            {
                tcs.SetException(new PlatformException($"Failed to request WebGpu adapter : {message}"));
                return;
            }

            tcs.SetResult(adapter);
        }, backendType);

        return tcs.Task;
    }

    public unsafe void Dispose()
    {
        _api.InstanceRelease(_handle);
        _handle = default;
    }
}

public delegate void RequestAdapterCallback(RequestAdapterStatus status, Adapter adapter, string message);