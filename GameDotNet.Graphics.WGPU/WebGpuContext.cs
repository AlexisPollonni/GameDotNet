using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using GameDotNet.Graphics.Abstractions;
using GameDotNet.Graphics.WGPU.Extensions;
using Microsoft.Extensions.Logging;
using Silk.NET.Core.Contexts;
using Silk.NET.Core.Native;
using Silk.NET.WebGPU;
using static Microsoft.Extensions.Logging.LogLevel;
using Adapter = GameDotNet.Graphics.WGPU.Wrappers.Adapter;
using AdapterProperties = GameDotNet.Graphics.WGPU.Wrappers.AdapterProperties;
using Device = GameDotNet.Graphics.WGPU.Wrappers.Device;
using Instance = GameDotNet.Graphics.WGPU.Wrappers.Instance;
using LogLevel = Silk.NET.WebGPU.Extensions.WGPU.LogLevel;
using Surface = GameDotNet.Graphics.WGPU.Wrappers.Surface;

namespace GameDotNet.Graphics.WGPU;

public sealed partial class WebGpuContext : IDisposable
{
    [MemberNotNullWhen(true, nameof(Surface), nameof(Adapter), nameof(Device))]
    public bool IsInitialized { get; private set; }

    public WebGPU Api { get; }
    public Instance Instance { get; }
    public Surface? Surface { get; private set; }
    public Adapter? Adapter { get; private set; }
    public Device? Device { get; private set; }


    private readonly ILogger<WebGpuContext> _logger;


    public WebGpuContext(ILogger<WebGpuContext> logger, ILoggerFactory loggerFactory)
    {
        _logger = logger;

        Api = WebGPU.GetApi();

        Instance = new(Api, loggerFactory.CreateLogger<Instance>());
    }

    [MemberNotNullWhen(true, nameof(Surface), nameof(Adapter), nameof(Device))]
    public async ValueTask<bool> Initialize(INativeView view, CancellationToken token = default)
    {
        if (IsInitialized) return true;
        
        Instance.SetLoggingCallback((type, message) =>
        {
            var fmtMessage = message.ReplaceLineEndings();

            var lvl = type switch
            {
                LogLevel.Off => None,
                LogLevel.Error => Error,
                LogLevel.Warn => Warning,
                LogLevel.Info => Information,
                LogLevel.Debug => Debug,
                LogLevel.Trace => Trace,
                LogLevel.Force32 => None,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
            
            WebGpuLogCallback(lvl, fmtMessage);
        });
        
        Instance.SetLogLevel(LogLevel.Trace);

        foreach (var a in Instance.EnumerateAdapters())
        {
            a.GetProperties(out var p);
                
            WebGpuAdapterFound(p);
        }
        

        var surface = CreateSurfaceFromView(Instance, view);

        var adapter = await Instance.RequestAdapterAsync(surface, PowerPreference.HighPerformance, false, BackendType.Undefined,
                token).WaitWhilePollingAsync(Instance, token);

        adapter.GetProperties(out var properties);
        WebGpuAdapterSelected(properties);
        
        adapter.GetLimits(out var limits);

        var device = await adapter.RequestDeviceAsync(limits: limits.Limits, label: "Device", nativeFeatures:
        [
            FeatureName.IndirectFirstInstance
        ], token: token);

        device.SetUncapturedErrorCallback((type, message) =>
        {
            WebGpuUncapturedError(type, message.ReplaceLineEndings());
        });

        device.Queue.OnSubmittedWorkDone(WebGpuSubmittedWorkDone);

        Surface = surface;
        Adapter = adapter;
        Device = device;

        IsInitialized = true;
        ResizeSurface(view.Size);
        return true;
    }

    public void Dispose()
    {
        Device?.Dispose();
        Adapter?.Dispose();
        Surface?.Dispose();
        Instance.Dispose();
        Api.Dispose();
    }

    public void ResizeSurface(Size size)
    {
        if (!IsInitialized) throw new InvalidOperationException("Context is not initialized");

        var fmt = Surface.GetPreferredFormat(Adapter);
        
        if (size.Height is 0 || size.Width is 0)
        {
            Surface.UnConfigure(Device);
        }
        else
            Surface.Configure(Device,
                new(fmt, TextureUsage.RenderAttachment, [fmt],
                    CompositeAlphaMode.Auto, size, PresentMode.Fifo));
    }

    private static unsafe Surface CreateSurfaceFromView(Instance instance, INativeView view)
    {
        var nat = view.Native ??
                  throw new PlatformNotSupportedException("No native view found to initialize WebGPU from");

        Surface surface;
        if (nat.Kind.HasFlag(NativeWindowFlags.Win32))
        {
            surface = instance.CreateSurfaceFromWindowsHWND((void*)nat.Win32!.Value.HInstance,
                (void*)nat.Win32.Value.Hwnd,
                "surface-create-Win32");
        }
        else if (nat.Kind.HasFlag(NativeWindowFlags.X11))
        {
            surface = instance.CreateSurfaceFromXlibWindow((void*)nat.X11!.Value.Display,
                (uint)nat.X11.Value.Window,
                "surface-create-X11");
        }
        else if (nat.Kind.HasFlag(NativeWindowFlags.Cocoa))
        {
            surface = instance.CreateSurfaceFromMetalLayer((void*)nat.Cocoa, "surface-create-MetalCocoa");
        }
        else throw new PlatformNotSupportedException("Native window type is not supported by webpgu");

        return surface;
    }

    [LoggerMessage(Error, Message = "[WebGPU][{ErrorType}: {Message}]")]
    private partial void WebGpuUncapturedError(ErrorType errorType, string message);

    [LoggerMessage(Information, Message = "[WebGPU] Found adapter {AdapterProperties}")]
    private partial void WebGpuAdapterFound(AdapterProperties adapterProperties);

    [LoggerMessage(Information, "[WebGpu] Selected adapter {AdapterProperties}")]
    private partial void WebGpuAdapterSelected(AdapterProperties adapterProperties);
    
    [LoggerMessage(Message = "[WebGPU] {Msg}")]
    private partial void WebGpuLogCallback(Microsoft.Extensions.Logging.LogLevel lvl, string msg);

    [LoggerMessage(Trace, Message = "[WebGPU] Queue submit {Status}")]
    private partial void WebGpuSubmittedWorkDone(QueueWorkDoneStatus status);
}