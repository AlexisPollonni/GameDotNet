using GameDotNet.Graphics.Vulkan.Tools;
using GameDotNet.Graphics.Vulkan.Tools.Extensions;
using Silk.NET.Vulkan;
using static GameDotNet.Graphics.Vulkan.Wrappers.VulkanCommandBufferPool;

namespace GameDotNet.Graphics.Vulkan.Wrappers;

public sealed class VulkanRenderPass : IDisposable
{
    public RenderPass Handle { get; }

    private readonly Vk _api;
    private readonly VulkanDevice _device;
    private readonly IVulkanAllocCallback _callbacks;

    public VulkanRenderPass(Vk api, VulkanDevice device, IVulkanAllocCallback callbacks, in RenderPassCreateInfo info)
    {
        _api = api;
        _device = device;
        _callbacks = callbacks;

        Handle = CreateRenderPass(info);
    }

    public static implicit operator RenderPass(VulkanRenderPass p) => p.Handle;

    public unsafe void Begin(VulkanCommandBuffer cmd, in Rect2D area, VulkanFramebuffer framebuffer,
                             ReadOnlySpan<ClearValue> clearValues)
    {
        fixed (ClearValue* pClear = clearValues)
        {
            var info = new RenderPassBeginInfo
            {
                SType = StructureType.RenderPassBeginInfo,
                RenderPass = Handle,
                Framebuffer = framebuffer,
                RenderArea = area,
                ClearValueCount = (uint)clearValues.Length,
                PClearValues = pClear
            };
            _api.CmdBeginRenderPass(cmd, info, SubpassContents.Inline);
        }
    }

    public void End(VulkanCommandBuffer cmd) => _api.CmdEndRenderPass(cmd);

    public void Dispose()
    {
        _api.DestroyRenderPass(_device, Handle, _callbacks.Handle);
    }

    private RenderPass CreateRenderPass(in RenderPassCreateInfo info)
    {
        _api.CreateRenderPass(_device, info, _callbacks.Handle, out var renderPass)
            .ThrowOnError("Failed to create render pass");

        return renderPass;
    }
}