using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Vulkan;
using GameDotNet.Core.Tooling;
using GameDotNet.Graphics.Abstractions;
using GameDotNet.Graphics.Vulkan.Abstractions;
using GameDotNet.Graphics.Vulkan.Bootstrap;
using GameDotNet.Graphics.Vulkan.Services;
using GameDotNet.Graphics.Vulkan.Tools.Extensions;
using GameDotNet.Graphics.Vulkan.Wrappers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nito.Disposables;
using Shouldly;
using Silk.NET.Core.Contexts;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace GameDotNet.Graphics.Vulkan.Tools;

public static class ServiceCollectionRegister
{
    public static unsafe IServiceCollection AddVulkanRenderer(this IServiceCollection services)
    {
        return services
            .AddSingleton<IVulkanContext, DefaultVulkanContext>(provider =>
            {
                var logger = provider.GetRequiredService<ILogger<DefaultVulkanContext>>();

                var contextFac = provider.GetRequiredService<IVulkanContextFactory>();
                var instanceExtensions = new List<string>
                {
                    KhrGetPhysicalDeviceProperties2.ExtensionName,
                    KhrExternalMemoryCapabilities.ExtensionName,
                    KhrExternalSemaphoreCapabilities.ExtensionName,
                };

                SelectionCriteria criteria = new()
                {
                    RequiredVersion = Vk.Version13,
                    DeferSurfaceInit = true,
                    PreferredType = PhysicalDeviceType.DiscreteGpu,
                    RequirePresent = true,
                    DesiredExtensions =
                    [
                        "VK_KHR_external_memory",
                        "VK_KHR_external_semaphore",
                        "VK_KHR_dedicated_allocation",
                    ],
                };

                criteria.ExtendedFeaturesChain = Chain.Create(
                    new PhysicalDeviceFeatures2(features: null),
                    new PhysicalDeviceVulkan12Features(timelineSemaphore: true),
                    new PhysicalDeviceVulkan13Features(
                        synchronization2: true,
                        dynamicRendering: true
                    )
                );

                if (OperatingSystem.IsWindows())
                {
                    criteria.DesiredExtensions.Add(KhrExternalMemoryWin32.ExtensionName);
                    criteria.DesiredExtensions.Add(KhrExternalSemaphoreWin32.ExtensionName);
                }
                else if (OperatingSystem.IsLinux())
                {
                    criteria.DesiredExtensions.Add(KhrExternalMemoryFd.ExtensionName);
                    criteria.DesiredExtensions.Add(KhrExternalSemaphoreFd.ExtensionName);

                    instanceExtensions.Add(KhrXlibSurface.ExtensionName);
                }
                else
                {
                    throw new PlatformNotSupportedException("Unsupported platform");
                }

                var context = contextFac.CreateFrom(criteria, instanceExtensions);

                var props = context.PhysDevice.Properties;
                logger.LogInformation(
                    "Created vulkan context with device {VulkanDevice}",
                    SilkMarshal.PtrToString((IntPtr)props.DeviceName)
                );

                return (DefaultVulkanContext)context;

                throw new InvalidOperationException("Failed to create Vulkan context");
            })
            .AddSingleton<GpuCompletionMonitor>()
            .AddKeyedSingleton<CommandSubmitter>(QueueFlags.GraphicsBit)
            .AddSingleton<IEntityRenderer, VulkanRenderer>()
            .AddSingleton<IVulkanDevice, AvaloniaVulkanDeviceWrapper>()
            .AddAutoFactories();
    }
}

public class AvaloniaVulkanDeviceWrapper : SingleDisposable<EmptyStruct>, IVulkanDevice
{
    private readonly IVulkanContext _context;
    private readonly Task<QueueHandle?> _avaloniaQueue;
    private readonly CancellationTokenSource _disposeTokenSource = new();

    public AvaloniaVulkanDeviceWrapper(IVulkanContext context)
        : base(default)
    {
        _context = context;

        _avaloniaQueue = context.Queues.GetFirstGraphic().AsTask();

        Instance = new AvaloniaVulkanInstanceWrapper(context);
    }

    protected override void Dispose(EmptyStruct context)
    {
        _disposeTokenSource.Dispose();
    }

    public object? TryGetFeature(Type featureType)
    {
        return null;
    }

    public IDisposable Lock()
    {
        return Disposable.Create(null);
    }

    public IntPtr Handle => _context.Device.Underlying.Handle;
    public IntPtr PhysicalDeviceHandle => _context.PhysDevice.Device.Underlying.Handle;
    public IntPtr MainQueueHandle => GetAvaloniaQueue().Underlying.Handle;

    public uint GraphicsQueueFamilyIndex => GetAvaloniaQueue().FamilyIndex;
    public IVulkanInstance Instance { get; }
    public bool IsLost => false; //TODO: Implement
    public IEnumerable<string> EnabledExtensions => _context.PhysDevice.ExtensionsToEnable;

    private DeviceQueue GetAvaloniaQueue()
    {
        QueueHandle? handle = null;
        if (
            !_avaloniaQueue.Wait(TimeSpan.FromMilliseconds(200), _disposeTokenSource.Token)
            || _avaloniaQueue.IsCanceled
        )
        {
            throw new OperationCanceledException();
        }

        if (_avaloniaQueue.IsCompletedSuccessfully)
            handle = _avaloniaQueue.Result;

        return handle?.Queue
            ?? throw new InvalidOperationException(
                "Cannot lock a vulkan graphics queue for avalonia"
            );
    }
}

public class AvaloniaVulkanInstanceWrapper(IVulkanContext context) : IVulkanInstance
{
    public IntPtr GetInstanceProcAddress(IntPtr instance, string name)
    {
        var pfn = context.Api.GetInstanceProcAddr(new(instance), name);

        return pfn;
    }

    public IntPtr GetDeviceProcAddress(IntPtr device, string name)
    {
        var pfn = context.Api.GetDeviceProcAddr(new(device), name);

        return pfn;
    }

    public IntPtr Handle => context.Instance.Underlying.Handle;
    public IEnumerable<string> EnabledExtensions => context.Instance.EnabledExtensions;
}
