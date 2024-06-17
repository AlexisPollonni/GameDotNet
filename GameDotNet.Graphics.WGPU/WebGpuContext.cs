using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using GameDotNet.Graphics.Abstractions;
using GameDotNet.Graphics.WGPU.Extensions;
using Microsoft.Extensions.Logging;
using Silk.NET.Core.Contexts;
using Silk.NET.WebGPU;
using Adapter = GameDotNet.Graphics.WGPU.Wrappers.Adapter;
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


    public WebGpuContext(ILogger<WebGpuContext> logger)
    {
        _logger = logger;

        Api = WebGPU.GetApi();

        Instance = new(Api);
    }

    [MemberNotNullWhen(true, nameof(Surface), nameof(Adapter), nameof(Device))]
    public async ValueTask<bool> Initialize(INativeView view, CancellationToken token = default)
    {
        if (IsInitialized) return true;

        var surface = CreateSurfaceFromView(Instance, view);

        var adapter =
            await Instance.RequestAdapterAsync(surface, PowerPreference.HighPerformance, false, BackendType.Vulkan,
                token).WaitWhilePollingAsync(Instance, token);

        adapter.GetProperties(out var properties);
        adapter.GetLimits(out var limits);

        var device = await adapter.RequestDeviceAsync(limits: limits.Limits, label: "Device", nativeFeatures:
        [
            FeatureName.IndirectFirstInstance
        ], token: token);

        device.SetUncapturedErrorCallback((type, message) =>
        {
            WebGpuUncapturedError(type, message.ReplaceLineEndings());
        });

        device.SetLoggingCallback((type, message) =>
        {
            var fmtMessage = message.ReplaceLineEndings();

            var lvl = type switch
            {
                LogLevel.Off => Microsoft.Extensions.Logging.LogLevel.None,
                LogLevel.Error => Microsoft.Extensions.Logging.LogLevel.Error,
                LogLevel.Warn => Microsoft.Extensions.Logging.LogLevel.Warning,
                LogLevel.Info => Microsoft.Extensions.Logging.LogLevel.Information,
                LogLevel.Debug => Microsoft.Extensions.Logging.LogLevel.Debug,
                LogLevel.Trace => Microsoft.Extensions.Logging.LogLevel.Trace,
                LogLevel.Force32 => Microsoft.Extensions.Logging.LogLevel.None,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
            
            WebGpuLogCallback(lvl, fmtMessage);
        });
        
        device.SetLogLevel(LogLevel.Trace);

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

    [LoggerMessage(Microsoft.Extensions.Logging.LogLevel.Error,
        Message = "[WebGPU][{ErrorType}: {Message}]")]
    private partial void WebGpuUncapturedError(ErrorType errorType, string message);
    
    [LoggerMessage(Message = "[WebGPU] {Msg}")]
    private partial void WebGpuLogCallback(Microsoft.Extensions.Logging.LogLevel lvl, string msg);

    [LoggerMessage(Microsoft.Extensions.Logging.LogLevel.Trace,
        Message = "[WebGPU] Queue submit {Status}")]
    private partial void WebGpuSubmittedWorkDone(QueueWorkDoneStatus status);
}