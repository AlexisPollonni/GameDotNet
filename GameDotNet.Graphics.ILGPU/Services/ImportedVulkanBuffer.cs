using ByteSizeLib;
using GameDotNet.Graphics.ILGPU.Tools;
using GameDotNet.Graphics.Vulkan.Services;
using GameDotNet.Graphics.Vulkan.Wrappers;
using ILGPU;
using ILGPU.Runtime;
using Silk.NET.Vulkan;

namespace GameDotNet.Graphics.ILGPU.Services;

internal sealed class ImportedVulkanBuffer(
    Accelerator accelerator,
    VulkanInteropExporter exporter,
    VulkanBuffer vkBuffer,
    bool disposeVkBuffer = false
) : AcceleratorObject(accelerator)
{
    public VulkanBuffer VkBuffer { get; } = vkBuffer;

    public MemoryBuffer Buffer { get; } =
        accelerator.ImportInteropBuffer(
            exporter.GetMemoryInteropHandle(vkBuffer),
            vkBuffer.Allocation.Size,
            ArrayView<byte>.ElementSize
        );

    public ByteSize Size => ByteSize.FromBytes(Buffer.LengthInBytes);

    public ImportedVulkanBuffer(
        Accelerator accelerator,
        VulkanInteropExporter exporter,
        ByteSize targetSize
    )
        : this(
            accelerator,
            exporter,
            exporter.CreateExportableBuffer(
                (ulong)targetSize.Bytes,
                BufferUsageFlags.TransferDstBit | BufferUsageFlags.TransferSrcBit
            ),
            true
        ) { }

    protected override void DisposeAcceleratorObject(bool disposing)
    {
        Buffer.Dispose();
        if (disposeVkBuffer)
            VkBuffer.Dispose();
    }

    public static implicit operator MemoryBuffer(ImportedVulkanBuffer buffer) => buffer.Buffer;
}