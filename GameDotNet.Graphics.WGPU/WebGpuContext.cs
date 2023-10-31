using System.Drawing;
using Microsoft.Extensions.Logging;
using Silk.NET.Core.Contexts;
using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Extensions.Dawn;
using Silk.NET.Windowing;
using Adapter = GameDotNet.Graphics.WGPU.Wrappers.Adapter;
using Device = GameDotNet.Graphics.WGPU.Wrappers.Device;
using Instance = GameDotNet.Graphics.WGPU.Wrappers.Instance;
using Surface = GameDotNet.Graphics.WGPU.Wrappers.Surface;
using SwapChain = GameDotNet.Graphics.WGPU.Wrappers.SwapChain;
using TextureFormat = Silk.NET.WebGPU.TextureFormat;

namespace GameDotNet.Graphics.WGPU;

public sealed class WebGpuContext : IDisposable
{
    public required WebGPU Api { get; init; }
    public required Instance Instance { get; init; }
    public required Surface Surface { get; init; }
    public required Adapter Adapter { get; init; }
    public required Device Device { get; init; }
    public required SwapChain SwapChain { get; init; }


    private readonly ILogger _logger;


    internal WebGpuContext(ILogger logger)
    {
        _logger = logger;
    }

    public static async ValueTask<WebGpuContext> Create(ILogger logger, IView view, CancellationToken token = default)
    {
        var api = WebGPU.GetApi();
        var instance = new Instance(api);

        var surface = CreateSurfaceFromView(instance, view);

        var adapter =
            await instance.RequestAdapterAsync(surface, PowerPreference.HighPerformance, false, BackendType.Vulkan,
                                               token);

        adapter.GetProperties(out var properties);
        adapter.GetLimits(out var limits);

        var device = await adapter.RequestDeviceAsync(limits: limits.Limits, label: "Device", nativeFeatures: new[]
        {
            FeatureName.IndirectFirstInstance
        }, token: token);

        device.SetUncapturedErrorCallback((type, message) =>
        {
            logger.LogError("[WebGPU][{ErrorType}: {Message}]", type, message);
        });

        device.SetLoggingCallback((type, message) =>
        {
            switch (type)
            {
                case LoggingType.Verbose:
                    logger.LogTrace("[WebGPU] {Msg}", message);
                    break;
                case LoggingType.Info:
                    logger.LogInformation("[WebGPU] {Msg}", message);
                    break;
                case LoggingType.Warning:
                    logger.LogWarning("[WebGPU] {Msg}", message);
                    break;
                case LoggingType.Error:
                    logger.LogError("[WebGPU] {Msg}", message);
                    break;
                case LoggingType.Force32:
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        });

        //device.Queue.OnSubmittedWorkDone(status => logger.LogDebug("[WebGPU] Queue submit {Status}", status));


        var sw = device.CreateSwapchain(surface, new(view.Size.X, view.Size.Y), TextureFormat.Bgra8Unorm,
                                        TextureUsage.RenderAttachment, PresentMode.Fifo, "create-swapchain");
        
        return new(logger)
        {
            Api = api,
            Instance = instance,
            Surface = surface,
            Adapter = adapter,
            Device = device,
            SwapChain = sw
        };
    }

    public void Dispose()
    {
        SwapChain.Dispose();
        Device.Dispose();
        Adapter.Dispose();
        Surface.Dispose();
        Instance.Dispose();
        Api.Dispose();
    }

    public void ResizeSurface(Size size) 
        => SwapChain.Configure(Device, Surface, size, TextureFormat.Bgra8Unorm, TextureUsage.RenderAttachment, PresentMode.Fifo);

    private static unsafe Surface CreateSurfaceFromView(Instance instance, INativeWindowSource view)
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