using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using GameDotNet.Graphics.Abstractions;
using GameDotNet.Graphics.WGPU.Extensions;
using Microsoft.Extensions.Logging;
using Silk.NET.Core.Contexts;
using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Extensions.Dawn;
using Adapter = GameDotNet.Graphics.WGPU.Wrappers.Adapter;
using Device = GameDotNet.Graphics.WGPU.Wrappers.Device;
using Instance = GameDotNet.Graphics.WGPU.Wrappers.Instance;
using Surface = GameDotNet.Graphics.WGPU.Wrappers.Surface;
using SwapChain = GameDotNet.Graphics.WGPU.Wrappers.SwapChain;
using TextureFormat = Silk.NET.WebGPU.TextureFormat;

namespace GameDotNet.Graphics.WGPU;

public sealed class WebGpuContext : IDisposable
{
    [MemberNotNullWhen(true, nameof(Surface), nameof(Adapter), nameof(Device), nameof(SwapChain))]
    public bool IsInitialized { get; private set; }
    public WebGPU Api { get; }
    public Instance Instance { get; }
    public Surface? Surface { get; private set; }
    public Adapter? Adapter { get; private set; }
    public Device? Device { get; private set; }
    public SwapChain? SwapChain { get; private set; }

    
    private readonly ILogger<WebGpuContext> _logger;
    

    public WebGpuContext(ILogger<WebGpuContext> logger)
    {
        _logger = logger;
        
        Api = WebGPU.GetApi();
        Instance = new(Api);
    }

    [MemberNotNullWhen(true, nameof(Surface), nameof(Adapter), nameof(Device), nameof(SwapChain))]
    public async ValueTask<bool> Initialize(INativeView view, CancellationToken token = default)
    {
        if (IsInitialized) return true;
        
        var surface = CreateSurfaceFromView(Instance, view);

        var adapter =
            await Instance.RequestAdapterAsync(surface, PowerPreference.HighPerformance, false, BackendType.Vulkan,
                                               token).WaitWhilePollingAsync(Instance, token);

        adapter.GetProperties(out var properties);
        adapter.GetLimits(out var limits);
        
        var device = await adapter.RequestDeviceAsync(limits: limits.Limits, label: "Device", nativeFeatures: new[]
        {
            FeatureName.IndirectFirstInstance
        }, token: token);

        device.SetUncapturedErrorCallback((type, message) =>
        {
            _logger.LogError("[WebGPU][{ErrorType}: {Message}]", type, message.ReplaceLineEndings());
        });

        device.SetLoggingCallback((type, message) =>
        {
            var fmtMessage = message.ReplaceLineEndings();
            switch (type)
            {
                case LoggingType.Verbose:
                    _logger.LogTrace("[WebGPU] {Msg}", fmtMessage);
                    break;
                case LoggingType.Info:
                    _logger.LogInformation("[WebGPU] {Msg}", fmtMessage);
                    break;
                case LoggingType.Warning:
                    _logger.LogWarning("[WebGPU] {Msg}", fmtMessage);
                    break;
                case LoggingType.Error:
                    _logger.LogError("[WebGPU] {Msg}", fmtMessage);
                    break;
                case LoggingType.Force32:
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        });

        //device.Queue.OnSubmittedWorkDone(status => logger.LogDebug("[WebGPU] Queue submit {Status}", status));


        var sw = device.CreateSwapchain(surface, view.Size, TextureFormat.Bgra8Unorm,
                                        TextureUsage.RenderAttachment, PresentMode.Fifo, "create-swapchain");

        Surface = surface;
        Adapter = adapter;
        Device = device;
        SwapChain = sw;
        
        
        IsInitialized = true;
        return true;
    }

    public void Dispose()
    {
        SwapChain?.Dispose();
        Device?.Dispose();
        Adapter?.Dispose();
        Surface?.Dispose();
        Instance.Dispose();
        Api.Dispose();
    }

    public void ResizeSurface(Size size)
    {
        if (!IsInitialized) throw new InvalidOperationException("Context is not initialized");
        
        SwapChain.Configure(Device, Surface, size, TextureFormat.Bgra8Unorm, TextureUsage.RenderAttachment,
                            PresentMode.Fifo);
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
}