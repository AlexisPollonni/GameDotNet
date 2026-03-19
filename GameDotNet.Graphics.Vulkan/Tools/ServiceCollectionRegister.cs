using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using Avalonia.Vulkan;
using GameDotNet.Core.Tooling;
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
        if (false)
            return featureType == typeof(IVulkanContextExternalObjectsFeature)
                ? new VulkanAvaloniaGpuInterop(context)
                : null;
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

internal unsafe class VulkanAvaloniaGpuInterop : IVulkanContextExternalObjectsFeature
{
    private readonly IVulkanContext _context;

    public VulkanAvaloniaGpuInterop(IVulkanContext context)
    {
        _context = context;

        var idProps = new PhysicalDeviceIDProperties
        {
            SType = StructureType.PhysicalDeviceIDProperties,
        };
        var props2 = new PhysicalDeviceProperties2
        {
            SType = StructureType.PhysicalDeviceProperties2,
            PNext = &idProps,
        };
        context.Api.GetPhysicalDeviceProperties2(context.PhysDevice.Device, &props2);

        DeviceUuid = new byte[16];
        new ReadOnlySpan<byte>(idProps.DeviceUuid, 16).CopyTo(DeviceUuid);

        if (idProps.DeviceLuidvalid)
        {
            DeviceLuid = new byte[8];
            new ReadOnlySpan<byte>(idProps.DeviceLuid, 8).CopyTo(DeviceLuid);
        }
    }

    public CompositionGpuImportedImageSynchronizationCapabilities GetSynchronizationCapabilities(
        string imageHandleType
    )
    {
        // For now semaphores only, see if we move towards timeline semaphores later
        return CompositionGpuImportedImageSynchronizationCapabilities.Semaphores;
    }

    public IVulkanExternalImage ImportImage(
        IPlatformHandle handle,
        PlatformGraphicsExternalImageProperties properties
    )
    {
        if (handle is not VulkanImageHandle imageHandle)
        {
            throw new ArgumentException("Invalid handle type", nameof(handle));
        }

        imageHandle.Image.TransitionLayout(
            _context.Pool,
            ImageLayout.ColorAttachmentOptimal,
            AccessFlags.ColorAttachmentReadBit | AccessFlags.ColorAttachmentWriteBit
        );
        return new SharedImage(imageHandle.Image, ImageLayout.ColorAttachmentOptimal);
    }

    public IVulkanExternalSemaphore ImportSemaphore(IPlatformHandle handle)
    {
        return handle is not VulkanSemaphoreHandle semaphoreHandle
            ? throw new ArgumentException("Invalid handle type", nameof(handle))
            : new SharedSemaphore(_context, semaphoreHandle.Semaphore);
    }

    public IReadOnlyList<string> SupportedImageHandleTypes { get; } = [nameof(VulkanImageHandle)];
    public IReadOnlyList<string> SupportedSemaphoreTypes { get; } = [nameof(VulkanSemaphoreHandle)];
    public byte[] DeviceUuid { get; }
    public byte[]? DeviceLuid { get; }

    private sealed class SharedImage(VulkanImage image, ImageLayout currentLayout)
        : SingleNonblockingDisposable<EmptyStruct>(default),
            IVulkanExternalImage
    {
        private readonly VulkanImageView _view = image.GetImageView(
            image.Format,
            ImageAspectFlags.ColorBit
        );

        protected override void Dispose(EmptyStruct context)
        {
            _view.Dispose();
        }

        public VulkanImageInfo Info =>
            new()
            {
                Format = (uint)image.Format,
                Handle = image.Image.Handle,
                Layout = (uint)currentLayout,
                MemoryHandle = image.Allocation.DeviceMemory.Handle,
                MemorySize = (ulong)image.Allocation.Size,
                PixelSize = new((int)image.Extent.Width, (int)image.Extent.Height),
                SampleCount = (uint)image.Description.SampleCount,
                Tiling = (uint)image.CreateInfo.Tiling,
                LevelCount = image.CreateInfo.MipLevels,
                IsProtected = (image.CreateInfo.Flags & ImageCreateFlags.CreateProtectedBit) != 0,
                UsageFlags = (uint)image.CreateInfo.Usage,
                ViewHandle = _view.ImageView.Handle,
            };
    }

    private sealed class SharedSemaphore(IVulkanContext context, VulkanSemaphore semaphore)
        : SingleNonblockingDisposable<EmptyStruct>(default),
            IVulkanExternalSemaphore
    {
        protected override void Dispose(EmptyStruct _)
        {
            // No need to dispose of anything, the underlying semaphore is owned by the context
        }

        public ulong Handle => semaphore.Handle.Handle;

        void SubmitSemaphore(VulkanSemaphore? wait, VulkanSemaphore? signal)
        {
            var buf = context.Pool.CreateCommandBuffer();
            buf.BeginRecording();
            context.Api.CmdPipelineBarrier(
                buf,
                PipelineStageFlags.AllCommandsBit,
                PipelineStageFlags.AllCommandsBit,
                DependencyFlags.None,
                0,
                null,
                0,
                null,
                0,
                null
            );

            buf.EndRecording();
            buf.Submit(wait, PipelineStageFlags.AllGraphicsBit, signal);
        }

        public void SubmitWaitSemaphore()
        {
            SubmitSemaphore(semaphore, null);
        }

        public void SubmitSignalSemaphore()
        {
            SubmitSemaphore(null, semaphore);
        }
    }

    public sealed class VulkanImageHandle(VulkanImage image) : IPlatformHandle
    {
        public VulkanImage Image => image;
        public IntPtr Handle => (IntPtr)image.Image.Handle;
        public string? HandleDescriptor => nameof(VulkanImageHandle);
    }

    public sealed class VulkanSemaphoreHandle(VulkanSemaphore semaphore) : IPlatformHandle
    {
        public VulkanSemaphore Semaphore => semaphore;
        public IntPtr Handle => (IntPtr)semaphore.Handle.Handle;
        public string? HandleDescriptor => nameof(VulkanSemaphoreHandle);
    }
}
