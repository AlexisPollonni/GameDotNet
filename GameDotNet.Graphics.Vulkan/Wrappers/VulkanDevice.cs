using GameDotNet.Core.Tooling;
using GameDotNet.Graphics.Vulkan.Abstractions;
using GameDotNet.Graphics.Vulkan.Services;
using Nito.Disposables;
using Silk.NET.Vulkan;

namespace GameDotNet.Graphics.Vulkan.Wrappers;

public sealed class VulkanDevice : SingleNonblockingDisposable<EmptyStruct>, IVulkanWrapper<Device>
{
    public Device Underlying { get; }
    public IVulkanContext Context { get; }

    public VulkanDevice(IVulkanContext context, Device device)
        : base(default)
    {
        Context = context;
        Underlying = device;
    }

    public static implicit operator Device(VulkanDevice device) => device.Underlying;

    protected override void Dispose(EmptyStruct context)
    {
        Context.Api.DestroyDevice(Underlying, in Context.Callbacks.Underlying);
    }
}
