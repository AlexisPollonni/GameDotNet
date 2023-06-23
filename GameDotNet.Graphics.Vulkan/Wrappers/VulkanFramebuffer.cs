using GameDotNet.Graphics.Vulkan.Tools.Allocators;
using GameDotNet.Graphics.Vulkan.Tools.Extensions;
using Silk.NET.Vulkan;

namespace GameDotNet.Graphics.Vulkan.Wrappers;

public sealed class VulkanFramebuffer : IDisposable
{
    public Framebuffer Handle { get; }

    private readonly Vk _api;
    private readonly VulkanDevice _device;
    private readonly IVulkanAllocCallback _callback;

    public unsafe VulkanFramebuffer(IVulkanContext ctx, Extent2D extent, VulkanRenderPass renderPass,
                                    ReadOnlySpan<ImageView> attachments,
                                    FramebufferCreateFlags flags = FramebufferCreateFlags.None, uint layers = 1)
    {
        _api = ctx.Api;
        _device = ctx.Device;
        _callback = ctx.Callbacks.WithUserData("Framebuffer");

        fixed (ImageView* pAttach = attachments)
        {
            var info = new FramebufferCreateInfo
            {
                SType = StructureType.FramebufferCreateInfo,
                Flags = flags,
                RenderPass = renderPass,
                AttachmentCount = (uint)attachments.Length,
                PAttachments = pAttach,
                Width = extent.Width,
                Height = extent.Height,
                Layers = layers
            };

            Handle = CreateFramebuffer(info);
        }
    }

    public VulkanFramebuffer(Vk api, VulkanDevice device, IVulkanAllocCallback callback, in FramebufferCreateInfo infos)
    {
        _api = api;
        _device = device;
        _callback = callback;
        Handle = CreateFramebuffer(infos);
    }

    public static implicit operator Framebuffer(VulkanFramebuffer b) => b.Handle;

    public void Dispose()
    {
        _api.DestroyFramebuffer(_device, Handle, _callback.Handle);
    }

    private Framebuffer CreateFramebuffer(in FramebufferCreateInfo info)
    {
        _api.CreateFramebuffer(_device, info, _callback.Handle, out var framebuffer)
            .ThrowOnError("Failed to create framebuffer");
        return framebuffer;
    }
}