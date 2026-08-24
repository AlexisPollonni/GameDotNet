using GameDotNet.Graphics.ILGPU.Abstractions;
using GameDotNet.Graphics.ILGPU.Tools;
using GameDotNet.Graphics.Vulkan.Services;
using GameDotNet.Graphics.Vulkan.Wrappers;
using ILGPU.Runtime;

namespace GameDotNet.Graphics.ILGPU.Services;

internal sealed class ImportedVulkanSemaphore(
    Accelerator accelerator,
    VulkanInteropExporter exporter,
    VulkanTimelineSemaphore vkSemaphore,
    bool disposeVkSemaphore = false
) : AcceleratorObject(accelerator)
{
    public VulkanTimelineSemaphore VkSemaphore { get; } = vkSemaphore;

    public IExternalSemaphore ImportedSemaphore { get; } =
        accelerator.ImportInteropSemaphore(exporter.GetSemaphoreInteropHandle(vkSemaphore));

    public ImportedVulkanSemaphore(Accelerator accelerator, VulkanInteropExporter exporter)
        : this(accelerator, exporter, exporter.CreateExportableSemaphore(), true) { }

    protected override void DisposeAcceleratorObject(bool disposing)
    {
        ImportedSemaphore.Dispose();
        if (disposeVkSemaphore)
            VkSemaphore.Dispose();
    }
}