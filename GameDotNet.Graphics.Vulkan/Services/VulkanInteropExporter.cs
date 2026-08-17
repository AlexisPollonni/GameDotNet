using GameDotNet.Graphics.Vulkan.Abstractions;
using GameDotNet.Graphics.Vulkan.MemoryAllocation;
using GameDotNet.Graphics.Vulkan.Tools.Extensions;
using GameDotNet.Graphics.Vulkan.Wrappers;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace GameDotNet.Graphics.Vulkan.Services;

/// <summary>
/// Provides functionality for interacting with Vulkan resources that are shared with external APIs.
/// This class facilitates the creation and export of Vulkan resources and
/// their associated handles in an OS-agnostic manner.
/// </summary>
public sealed unsafe class VulkanInteropExporter
{
    private readonly IVulkanContext _context;

    private readonly KhrExternalMemoryCapabilities? _extMemoryCapabilities;
    private readonly KhrExternalSemaphoreCapabilities? _extSemaphoreCapabilities;

    public VulkanInteropExporter(IVulkanContext context)
    {
        _context = context;

        _extMemoryCapabilities = _context.GetExtension<KhrExternalMemoryCapabilities>();
        _extSemaphoreCapabilities = _context.GetExtension<KhrExternalSemaphoreCapabilities>();
    }

    /// <summary>
    /// Creates a linear buffer and allocates exported device-local memory for it.
    /// This strictly packs memory linearly so external apis can write to it predictably.
    /// </summary>
    public VulkanBuffer CreateExportableBuffer(
        ulong sizeInBytes,
        BufferUsageFlags usageFlags =
            BufferUsageFlags.TransferSrcBit | BufferUsageFlags.StorageBufferBit,
        BufferCreateFlags bufferFlags = BufferCreateFlags.None
    )
    {
        var handleType = GetSupportedMemoryExportHandleType(bufferFlags, usageFlags);

        // TransferSrc allows copying to the presentation image later.
        // StorageBuffer allows compute shaders to read/write if we ever mix Vulkan compute in.
        var bufferCreateInfo = new BufferCreateInfo( //TODO: verify this still works and the flags are correct
            size: sizeInBytes,
            usage: usageFlags,
            flags: bufferFlags,
            sharingMode: SharingMode.Exclusive
        );
        var externalBufferInfo = new ExternalMemoryBufferCreateInfo(handleTypes: handleType);

        var buffer = new VulkanBuffer(
            _context.Allocator,
            in bufferCreateInfo.SetNext(ref externalBufferInfo),
            new(
                AllocationCreateFlags.DedicatedMemory,
                usage: MemoryUsage.GPU_Only,
                memoryAllocateNext: Chain.Create(
                    new MemoryAllocateInfo(),
                    new ExportMemoryAllocateInfo(handleTypes: handleType)
                )
            )
        );

        return buffer;
    }

    /// <summary>
    /// Extracts the OS-level memory handle to pass to other APIs.
    /// Note: Cast the returned IntPtr to an `int` (File Descriptor) on Linux.
    /// </summary>
    public nint GetMemoryInteropHandle(VulkanBuffer buffer)
    {
        var handleType = GetSupportedMemoryExportHandleType(
            buffer.CreateInfo.Flags,
            buffer.CreateInfo.Usage
        );

        switch (handleType)
        {
            case ExternalMemoryHandleTypeFlags.OpaqueWin32Bit:
                var win32Info = new MemoryGetWin32HandleInfoKHR(
                    memory: buffer.Allocation.DeviceMemory,
                    handleType: handleType
                );

                _context
                    .GetExtension<KhrExternalMemoryWin32>()
                    .GetMemoryWin32Handle(_context.Device, in win32Info, out var handle);
                return handle;
            case ExternalMemoryHandleTypeFlags.OpaqueFDBit:
                var fdInfo = new MemoryGetFdInfoKHR(
                    memory: buffer.Allocation.DeviceMemory,
                    handleType: handleType
                );

                _context
                    .GetExtension<KhrExternalMemoryFd>()
                    .GetMemoryF(_context.Device, in fdInfo, out var fd);
                return fd; // Box the FD into an IntPtr for interop struct passing
            default:
                throw new PlatformNotSupportedException(
                    $"Unsupported memory handle type {handleType}"
                );
        }
    }

    /// <summary>
    /// Creates a semaphore that can be exported to synchronize
    /// execution between Vulkan queue operations and other apis.
    /// </summary>
    public VulkanTimelineSemaphore CreateExportableSemaphore()
    {
        var handleType = GetSupportedSemaphoreExportHandleType(true);

        return handleType == ExternalSemaphoreHandleTypeFlags.None
            ? throw new PlatformNotSupportedException("No timeline semaphore export handle type")
            : new ExportableVulkanTimelineSemaphore(_context, handleType);
    }

    /// <summary>
    /// Extracts the OS-level semaphore handle to pass to other APIs.
    /// Note: Cast the returned IntPtr to an `int` (File Descriptor) on Linux.
    /// </summary>
    public nint GetSemaphoreInteropHandle(VulkanSemaphore semaphore)
    {
        var handleType = GetSupportedSemaphoreExportHandleType(
            semaphore is VulkanTimelineSemaphore
        );

        switch (handleType)
        {
            case ExternalSemaphoreHandleTypeFlags.OpaqueWin32Bit:
                var win32Info = new SemaphoreGetWin32HandleInfoKHR(
                    semaphore: semaphore,
                    handleType: ExternalSemaphoreHandleTypeFlags.OpaqueWin32Bit
                );

                _context
                    .GetExtension<KhrExternalSemaphoreWin32>()
                    .GetSemaphoreWin32Handle(_context.Device, in win32Info, out var handle)
                    .ThrowOnError();
                return handle;

            case ExternalSemaphoreHandleTypeFlags.OpaqueFDBit:
                var fdInfo = new SemaphoreGetFdInfoKHR(
                    semaphore: semaphore,
                    handleType: ExternalSemaphoreHandleTypeFlags.OpaqueFDBit
                );

                _context
                    .GetExtension<KhrExternalSemaphoreFd>()
                    .GetSemaphoreF(_context.Device, in fdInfo, out var fd)
                    .ThrowOnError();

                return fd;

            default:
                throw new PlatformNotSupportedException(
                    $"Unsupported semaphore handle type {handleType}"
                );
        }
    }

    public ExternalSemaphoreHandleTypeFlags GetSupportedSemaphoreExportHandleType(
        bool isTimeline = false
    )
    {
        if (_extSemaphoreCapabilities is null)
        {
            return ExternalSemaphoreHandleTypeFlags.None;
        }

        // Try NT handle first (modern, preferred)
        ReadOnlySpan<ExternalSemaphoreHandleTypeFlags> candidates =
        [
            ExternalSemaphoreHandleTypeFlags.OpaqueWin32Bit,
            ExternalSemaphoreHandleTypeFlags.OpaqueWin32KmtBit,
            ExternalSemaphoreHandleTypeFlags.OpaqueFDBit,
            //TODO: Support more handle types
        ];

        foreach (var candidate in candidates)
        {
            var info = new PhysicalDeviceExternalSemaphoreInfo(handleType: candidate);
            var timelineInfo = new SemaphoreTypeCreateInfo(semaphoreType: SemaphoreType.Timeline);

            if (isTimeline)
            {
                info.SetNext(ref timelineInfo);
            }

            _extSemaphoreCapabilities.GetPhysicalDeviceExternalSemaphoreProperties(
                _context.PhysDevice.Device,
                in info,
                out var props
            );

            if (
                (props.ExternalSemaphoreFeatures & ExternalSemaphoreFeatureFlags.ExportableBit) != 0
            )
            {
                return candidate;
            }
        }

        return ExternalSemaphoreHandleTypeFlags.None; // no exportable handle type found
    }

    public ExternalMemoryHandleTypeFlags GetSupportedMemoryExportHandleType(
        BufferCreateFlags bufferFlags,
        BufferUsageFlags bufferUsage
    )
    {
        if (_extMemoryCapabilities is null)
        {
            return ExternalMemoryHandleTypeFlags.None;
        }

        ReadOnlySpan<ExternalMemoryHandleTypeFlags> candidates =
        [
            ExternalMemoryHandleTypeFlags.OpaqueWin32Bit,
            ExternalMemoryHandleTypeFlags.OpaqueWin32KmtBit,
            ExternalMemoryHandleTypeFlags.OpaqueFDBit,
        ];

        foreach (var candidate in candidates)
        {
            var info = new PhysicalDeviceExternalBufferInfo(
                handleType: candidate,
                flags: bufferFlags,
                usage: bufferUsage
            );

            _extMemoryCapabilities.GetPhysicalDeviceExternalBufferProperties(
                _context.PhysDevice.Device,
                in info,
                out var props
            );

            if (
                props.ExternalMemoryProperties.ExternalMemoryFeatures.HasFlag(
                    ExternalMemoryFeatureFlags.ExportableBit
                )
            )
            {
                return info.HandleType;
            }
        }

        return ExternalMemoryHandleTypeFlags.None;
    }
}
