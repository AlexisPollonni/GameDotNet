using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Vulkan;
using GameDotNet.Graphics.Abstractions;
using GameDotNet.Graphics.Vulkan.Bootstrap;
using GameDotNet.Graphics.Vulkan.Tools.Extensions;
using GameDotNet.Graphics.Vulkan.Wrappers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nito.Disposables;
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

                SelectionCriteria criteria = new()
                {
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

                if (OperatingSystem.IsWindows())
                {
                    criteria.DesiredExtensions.Add("VK_KHR_external_memory_win32");
                    criteria.DesiredExtensions.Add("VK_KHR_external_semaphore_win32");
                }
                else if (OperatingSystem.IsLinux())
                {
                    criteria.DesiredExtensions.Add("VK_KHR_external_memory_fd");
                    criteria.DesiredExtensions.Add("VK_KHR_external_semaphore_fd");
                }
                else
                {
                    throw new PlatformNotSupportedException("Unsupported platform");
                }

                var context = contextFac.CreateFrom(
                    criteria,
                    [
                        KhrGetPhysicalDeviceProperties2.ExtensionName,
                        KhrExternalMemoryCapabilities.ExtensionName,
                        KhrExternalSemaphoreCapabilities.ExtensionName,
                    ]
                );

                {
                    var props = context.PhysDevice.Properties;
                    logger.LogInformation(
                        "Created vulkan context with device {VulkanDevice}",
                        SilkMarshal.PtrToString((IntPtr)props.DeviceName)
                    );

                    return (DefaultVulkanContext)context;
                }

                throw new InvalidOperationException("Failed to create Vulkan context");
            })
            .AddSingleton<IEntityRenderer, VulkanRenderer>()
            .AddSingleton<IVulkanDevice, AvaloniaVulkanDeviceWrapper>()
            .AddAutoFactories();
    }
}

public class AvaloniaVulkanDeviceWrapper(IVulkanContext context) : IVulkanDevice
{
    private readonly Lock _lock = new();

    public void Dispose()
    {
        //noop
    }

    public object? TryGetFeature(Type featureType)
    {
        return null;
    }

    public IDisposable Lock()
    {
        _lock.Enter();
        return Disposable.Create(() => _lock.Exit());
    }

    public IntPtr Handle => context.Device.Underlying.Handle;
    public IntPtr PhysicalDeviceHandle => context.PhysDevice.Device.Underlying.Handle;
    public IntPtr MainQueueHandle =>
        context.Device.QueuesManager.GetFirstGraphic()?.Handle.Handle ?? IntPtr.Zero;
    public uint GraphicsQueueFamilyIndex =>
        (uint)(context.Device.QueuesManager.GetFirstGraphic()?.FamilyIndex ?? 0);
    public IVulkanInstance Instance { get; } = new AvaloniaVulkanInstanceWrapper(context);
    public bool IsLost { get; } = false; //TODO: Implement
    public IEnumerable<string> EnabledExtensions => context.PhysDevice.ExtensionsToEnable;
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
