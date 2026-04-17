using GameDotNet.Graphics.Vulkan.Abstractions;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace GameDotNet.Graphics.Vulkan.Wrappers;

public class DeviceQueue(
    IVulkanContext context,
    Queue deviceQueue,
    uint familyIndex,
    uint queueIndex
) : IVulkanWrapper<Queue>
{
    public IVulkanContext Context { get; } = context;
    public Queue Underlying { get; } = deviceQueue;
    public uint FamilyIndex { get; } = familyIndex;
    public uint QueueIndex { get; } = queueIndex;

    //TODO: private ThreadOwnershipGuard _guard;

    public static implicit operator Queue(DeviceQueue q) => q.Underlying;

    public unsafe Result Submit(
        ReadOnlySpan<SemaphoreSubmitInfo> waitSemaphoreInfos,
        ReadOnlySpan<CommandBufferSubmitInfo> commandBufferInfos,
        ReadOnlySpan<SemaphoreSubmitInfo> signalSemaphoreInfos
    )
    {
        fixed (
            SemaphoreSubmitInfo* pWaitSemaphores = waitSemaphoreInfos,
                pSignalSemaphores = signalSemaphoreInfos
        )
        fixed (CommandBufferSubmitInfo* pCommandBuffers = commandBufferInfos)
        {
            var submitInfo = new SubmitInfo2(
                pCommandBufferInfos: pCommandBuffers,
                commandBufferInfoCount: (uint)commandBufferInfos.Length,
                pWaitSemaphoreInfos: pWaitSemaphores,
                waitSemaphoreInfoCount: (uint)waitSemaphoreInfos.Length,
                pSignalSemaphoreInfos: pSignalSemaphores,
                signalSemaphoreInfoCount: (uint)signalSemaphoreInfos.Length,
                flags: SubmitFlags.None
            );

            return Submit([submitInfo]);
        }
    }

    public Result Submit(ReadOnlySpan<SubmitInfo2> submitInfos, Fence? fence = null)
    {
        return Context.Api.QueueSubmit2(Underlying, submitInfos, fence ?? default);
    }

    public Result BindSparse(ReadOnlySpan<BindSparseInfo> bindSparseInfos, Fence? fence = null)
    {
        return Context.Api.QueueBindSparse(Underlying, bindSparseInfos, fence ?? default);
    }

    public Result Present(KhrSwapchain extension, in PresentInfoKHR info)
    {
        return extension.QueuePresent(Underlying, in info);
    }

    public Result WaitIdle()
    {
        return Context.Api.QueueWaitIdle(Underlying);
    }
}
