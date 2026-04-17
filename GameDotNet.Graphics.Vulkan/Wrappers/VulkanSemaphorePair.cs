using GameDotNet.Core.Tooling;
using GameDotNet.Graphics.Vulkan.Abstractions;
using GameDotNet.Graphics.Vulkan.Tools.Extensions;
using Nito.Disposables;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace GameDotNet.Graphics.Vulkan.Wrappers;

public sealed class VulkanSemaphorePair : SingleNonblockingDisposable<EmptyStruct>
{
    public VulkanSemaphore ImageAvailableSemaphore { get; }
    public VulkanSemaphore RenderFinishedSemaphore { get; }

    private readonly IVulkanContext _context;

    public unsafe VulkanSemaphorePair(IVulkanContext context, bool exportable)
        : base(default)
    {
        _context = context;

        var supported = GetSupportedSemaphoreExportHandleType(_context);

        var semaphoreExportInfo = new ExportSemaphoreCreateInfo(
            handleTypes: supported
                ?? throw new InvalidOperationException(
                    "The physical device does not support exportable semaphores."
                )
        );
        var semaphoreCreateInfo = new SemaphoreCreateInfo(
            pNext: exportable ? &semaphoreExportInfo : null
        );

        ImageAvailableSemaphore = new(context, in semaphoreCreateInfo);
        RenderFinishedSemaphore = new(context, in semaphoreCreateInfo);
    }

    public static unsafe ExternalSemaphoreHandleTypeFlags? GetSupportedSemaphoreExportHandleType(
        IVulkanContext context
    )
    {
        if (
            !context.Api.TryGetInstanceExtension<KhrExternalSemaphoreCapabilities>(
                context.Instance,
                out var ext
            )
        )
        {
            return null;
        }

        // Try NT handle first (modern, preferred)
        ExternalSemaphoreHandleTypeFlags[] candidates = OperatingSystem.IsWindows()
            ?
            [
                ExternalSemaphoreHandleTypeFlags.OpaqueWin32Bit,
                ExternalSemaphoreHandleTypeFlags.OpaqueWin32KmtBit,
            ]
            : [ExternalSemaphoreHandleTypeFlags.OpaqueFDBit];

        foreach (var candidate in candidates)
        {
            var info = new PhysicalDeviceExternalSemaphoreInfo
            {
                SType = StructureType.PhysicalDeviceExternalSemaphoreInfo,
                HandleType = candidate,
            };
            var props = new ExternalSemaphoreProperties
            {
                SType = StructureType.ExternalSemaphoreProperties,
            };

            ext.GetPhysicalDeviceExternalSemaphoreProperties(
                context.PhysDevice.Device,
                &info,
                &props
            );

            if (
                (props.ExternalSemaphoreFeatures & ExternalSemaphoreFeatureFlags.ExportableBit) != 0
            )
            {
                return candidate;
            }
        }

        return null; // no exportable handle type found
    }

    public nint Export(bool renderFinished)
    {
        if (OperatingSystem.IsWindows())
        {
            var win32Ext = _context.GetExtension<KhrExternalSemaphoreWin32>();

            var info = new SemaphoreGetWin32HandleInfoKHR
            {
                SType = StructureType.SemaphoreGetWin32HandleInfoKhr,
                Semaphore = renderFinished ? RenderFinishedSemaphore : ImageAvailableSemaphore,
                HandleType = ExternalSemaphoreHandleTypeFlags.OpaqueWin32Bit,
            };

            win32Ext
                .GetSemaphoreWin32Handle(_context.Device, in info, out var handle)
                .ThrowOnError();
            return handle;
        }

        if (OperatingSystem.IsLinux())
        {
            var fdExt = _context.GetExtension<KhrExternalSemaphoreFd>();
            var info = new SemaphoreGetFdInfoKHR
            {
                SType = StructureType.SemaphoreGetFDInfoKhr,
                Semaphore = renderFinished ? RenderFinishedSemaphore : ImageAvailableSemaphore,
                HandleType = ExternalSemaphoreHandleTypeFlags.OpaqueFDBit,
            };
            fdExt.GetSemaphoreF(_context.Device, in info, out var fd).ThrowOnError();
            return fd;
        }

        throw new PlatformNotSupportedException(
            "External semaphore export is not supported on this platform."
        );
    }

    protected override void Dispose(EmptyStruct context)
    {
        _context.Api.DestroySemaphore(
            _context.Device,
            ImageAvailableSemaphore,
            in _context.Callbacks.Handle
        );
        _context.Api.DestroySemaphore(
            _context.Device,
            RenderFinishedSemaphore,
            in _context.Callbacks.Handle
        );
    }
}
