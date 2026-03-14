using GameDotNet.Graphics.Vulkan.Tools.Extensions;
using Silk.NET.Vulkan;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace GameDotNet.Graphics.Vulkan.Wrappers;

public sealed class VulkanSemaphore : IDisposable
{
    public Semaphore Handle { get; }

    private readonly IVulkanContext _context;

    public VulkanSemaphore(IVulkanContext context)
        : this(
            context,
            new() { SType = StructureType.SemaphoreCreateInfo, Flags = SemaphoreCreateFlags.None }
        ) { }

    public VulkanSemaphore(IVulkanContext context, in SemaphoreCreateInfo info)
    {
        _context = context;

        context
            .Api.CreateSemaphore(context.Device, in info, in context.Callbacks.Handle, out var sem)
            .ThrowOnError("Unable to create semaphore");

        Handle = sem;
    }

    public static implicit operator Semaphore(VulkanSemaphore s) => s.Handle;

    public void Dispose()
    {
        _context.Api.DestroySemaphore(_context.Device, Handle, in _context.Callbacks.Handle);
    }
}
