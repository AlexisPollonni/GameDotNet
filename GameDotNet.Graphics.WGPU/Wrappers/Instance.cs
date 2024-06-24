using GameDotNet.Core.Tools.Containers;
using GameDotNet.Graphics.WGPU.Extensions;
using Microsoft.Extensions.Logging;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace GameDotNet.Graphics.WGPU.Wrappers;

public sealed partial class Instance : IDisposable
{
    private readonly WebGPU _api;
    private readonly ILogger<Instance> _logger;
    private unsafe Silk.NET.WebGPU.Instance* _handle;

    public Instance(WebGPU api, ILogger<Instance> logger)
    {
        _api = api;
        _logger = logger;

        unsafe
        {
            var desc = new InstanceDescriptor();
            _handle = api.CreateInstance(&desc);
        }
    }

    public void SetLogLevel(Silk.NET.WebGPU.Extensions.WGPU.LogLevel level) =>
        _api.GetWgpuExtension()?.SetLogLevel(level);

    public unsafe void SetLoggingCallback(LoggingCallback proc)
    {
        _api.GetWgpuExtension()
            ?.SetLogCallback(new((lvl, msgB, _) => proc(lvl, SilkMarshal.PtrToString((IntPtr)msgB)!)), null);
    }

    public unsafe IEnumerable<Adapter> EnumerateAdapters()
    {
        var ext = _api.GetWgpuExtension();

        var count = (int)ext.InstanceEnumerateAdapters(_handle, null, null);

        var pAdapters = stackalloc Silk.NET.WebGPU.Adapter*[count];

        ext.InstanceEnumerateAdapters(_handle, null, pAdapters);

        var l = new List<Adapter>(count);
        for (var i = 0; i < count; i++)
        {
            var adapter = new Adapter(_api, pAdapters[i]);
            l.Add(adapter);
        }

        return l;
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

    public unsafe void ProcessEvents()
    {
        _api.InstanceProcessEvents(_handle);
    }

    public unsafe void RequestAdapter(Surface compatibleSurface, PowerPreference powerPreference,
        bool forceFallbackAdapter, RequestAdapterCallback callback,
        BackendType backendType)
    {
        var cb = new PfnRequestAdapterCallback((s, a, m, _) =>
        {
            var msg = SilkMarshal.PtrToString((nint)m)!;
            if (s is not RequestAdapterStatus.Success)
                WepGpuRequestAdapterNotSuccess(s, msg);
            callback(s, new(_api, a), msg);
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

    [LoggerMessage(LogLevel.Warning,
        Message = "[WebGPU] Requested adapter status is {Status} : {Message}")]
    private partial void WepGpuRequestAdapterNotSuccess(RequestAdapterStatus status, string message);
}