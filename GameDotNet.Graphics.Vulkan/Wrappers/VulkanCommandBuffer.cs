using GameDotNet.Graphics.Vulkan.Abstractions;
using GameDotNet.Graphics.Vulkan.Tools.Extensions;
using Silk.NET.Vulkan;

namespace GameDotNet.Graphics.Vulkan.Wrappers;

public sealed class VulkanCommandBuffer : IVulkanWrapper<CommandBuffer>
{
    public IVulkanContext Context { get; }
    public CommandBuffer Underlying { get; }
    public VulkanCommandBufferPool Pool { get; }

    private bool _hasEnded;
    private bool _hasStarted;

    internal VulkanCommandBuffer(
        IVulkanContext context,
        CommandBuffer underlying,
        VulkanCommandBufferPool pool
    )
    {
        Context = context;
        Underlying = underlying;
        Pool = pool;
    }

    public static implicit operator CommandBuffer(VulkanCommandBuffer buffer) => buffer.Underlying;

    public unsafe void BeginRecording(
        CommandBufferUsageFlags flags = CommandBufferUsageFlags.OneTimeSubmitBit,
        ReadOnlySpan<CommandBufferInheritanceInfo> inheritanceInfo = default
    )
    {
        if (_hasStarted)
            return;

        fixed (CommandBufferInheritanceInfo* inheritanceInfoPtr = inheritanceInfo)
        {
            var beginInfo = new CommandBufferBeginInfo(
                flags: flags,
                pInheritanceInfo: inheritanceInfoPtr
            );

            Context.Api.BeginCommandBuffer(Underlying, in beginInfo).ThrowOnError();
        }

        _hasStarted = true;
    }

    public void EndRecording()
    {
        if (!_hasStarted || _hasEnded)
            return;

        _hasEnded = true;

        Context.Api.EndCommandBuffer(Underlying).ThrowOnError();
    }

    public void ResetState()
    {
        _hasStarted = false;
        _hasEnded = false;
    }
}
