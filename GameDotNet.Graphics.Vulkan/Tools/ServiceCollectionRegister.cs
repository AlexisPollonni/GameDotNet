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
        (
            context.Device.QueuesManager.GetLastQueueOrNew((int)GraphicsQueueFamilyIndex, true)
            ?? context.Device.QueuesManager.GetFirstGraphic().ShouldNotBeNull()
        )
            .Handle
            .Handle;
    public uint GraphicsQueueFamilyIndex =>
        (uint)(
            context.Device.QueuesManager.GetFirstGraphic()?.FamilyIndex
            ?? throw new InvalidOperationException("Vulkan device has no graphics queue family")
        );
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
