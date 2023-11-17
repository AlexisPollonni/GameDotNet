using GameDotNet.Graphics.Vulkan.Tools;
using GameDotNet.Graphics.Vulkan.Tools.Extensions;
using Silk.NET.Vulkan;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace GameDotNet.Graphics.Vulkan.Wrappers;

public sealed class VulkanSemaphore : IDisposable
{
    public Semaphore Handle { get; }


    private readonly Vk _api;
    private readonly VulkanDevice _device;
    private readonly IVulkanAllocCallback _callbacks;

    public VulkanSemaphore(Vk api, VulkanDevice device, IVulkanAllocCallback callbacks)
    {
        _api = api;
        _device = device;
        _callbacks = callbacks;

        var info = new SemaphoreCreateInfo
        {
            SType = StructureType.SemaphoreCreateInfo,
            Flags = SemaphoreCreateFlags.None
        };
        api.CreateSemaphore(device, info, callbacks.Handle, out var sem)
           .ThrowOnError("Unable to create semaphore");

        Handle = sem;
    }

    public static implicit operator Semaphore(VulkanSemaphore s) => s.Handle;

    public void Dispose()
    {
        _api.DestroySemaphore(_device, Handle, _callbacks.Handle);
    }
}