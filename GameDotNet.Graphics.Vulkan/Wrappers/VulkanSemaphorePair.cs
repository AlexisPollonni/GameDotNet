using GameDotNet.Graphics.Vulkan.Tools.Extensions;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace GameDotNet.Graphics.Vulkan.Wrappers;

public sealed class VulkanSemaphorePair : IDisposable
{
    private readonly VulkanInstance _instance;
    private readonly VulkanDevice _device;

    public unsafe VulkanSemaphorePair(VulkanInstance instance, VulkanDevice device, bool exportable)
    {
        _instance = instance;
        _device = device;
        
        var semaphoreExportInfo = new ExportSemaphoreCreateInfo(handleTypes:ExternalSemaphoreHandleTypeFlags.OpaqueFDBit);
        var semaphoreCreateInfo = new SemaphoreCreateInfo(pNext:exportable ? &semaphoreExportInfo : null);

        instance.Vk.CreateSemaphore(_device, semaphoreCreateInfo, null, out var semaphore).ThrowOnError();
        ImageAvailableSemaphore = semaphore;

        _instance.Vk.CreateSemaphore(_device, semaphoreCreateInfo, null, out semaphore).ThrowOnError();
        RenderFinishedSemaphore = semaphore;
    }

    public int ExportFd(bool renderFinished)
    {
        if (!_instance.Vk.TryGetDeviceExtension<KhrExternalSemaphoreFd>(_instance, _device,
                out var ext))
            throw new InvalidOperationException();
        var info = new SemaphoreGetFdInfoKHR()
        {
            SType = StructureType.SemaphoreGetFDInfoKhr,
            Semaphore = renderFinished ? RenderFinishedSemaphore : ImageAvailableSemaphore,
            HandleType = ExternalSemaphoreHandleTypeFlags.OpaqueFDBit
        };
        ext.GetSemaphoreF(_device, info, out var fd).ThrowOnError();
        return fd;
    }

    public Semaphore ImageAvailableSemaphore { get; }
    public Semaphore RenderFinishedSemaphore { get; }

    public unsafe void Dispose()
    {
        _instance.Vk.DestroySemaphore(_device, ImageAvailableSemaphore, null);
        _instance.Vk.DestroySemaphore(_device, RenderFinishedSemaphore, null);
    }
}