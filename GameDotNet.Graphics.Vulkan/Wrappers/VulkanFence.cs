using GameDotNet.Graphics.Vulkan.Tools.Allocators;
using GameDotNet.Graphics.Vulkan.Tools.Extensions;
using Silk.NET.Vulkan;

namespace GameDotNet.Graphics.Vulkan.Wrappers;

public sealed class VulkanFence : IDisposable
{
    public Fence Handle { get; }

    private readonly Vk _api;
    private readonly VulkanDevice _device;
    private readonly IVulkanAllocCallback _callbacks;

    public unsafe VulkanFence(Vk api, VulkanDevice device, FenceCreateFlags flags, IVulkanAllocCallback callbacks)
    {
        _api = api;
        _device = device;
        _callbacks = callbacks;

        var infos = new FenceCreateInfo(flags: flags);
        api.CreateFence(device, infos, callbacks.Handle, out var fence)
           .ThrowOnError("Couldn't create fence for device");

        Handle = fence;
    }

    public static implicit operator Fence(VulkanFence f) => f.Handle;

    public void Wait(ulong timeout = ulong.MaxValue)
    {
        _api.WaitForFences(_device, 1, Handle, true, timeout)
            .LogWarning("Vulkan fence wait failure");
    }

    public void Reset()
    {
        _api.ResetFences(_device, 1, Handle).ThrowOnError("Failed to reset fence");
    }

    public void Dispose()
    {
        _api.DestroyFence(_device, Handle, _callbacks.Handle);
    }
}