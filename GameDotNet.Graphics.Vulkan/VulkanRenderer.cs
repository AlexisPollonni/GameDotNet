using Arch.Core;
using GameDotNet.Graphics.Abstractions;
using GameDotNet.Graphics.Tooling;
using GameDotNet.Graphics.Vulkan.Wrappers;
using Silk.NET.Vulkan;

namespace GameDotNet.Graphics.Vulkan;

public sealed class VulkanRenderer(IVulkanContext context, TimeProvider provider) : IEntityRenderer
{
    private ulong _frameNumber;
    private const Format DepthFormat = Format.D32Sfloat;
    private readonly TimingsRingBuffer _timings = new(100);

    public unsafe TimelineStats Render(
        IDeviceTexture targetTexture,
        params ReadOnlySpan<Entity> entities
    )
    {
        var startTimestamp = provider.GetTimestamp();
        using var cmd = context.Pool.CreateCommandBuffer();
        cmd.BeginRecording();

        // make a clear-color from frame number. This will flash with a 120*pi frame period.
        var clearValue = new ClearValue(
            new(0, 0, (float)Math.Abs(Math.Sin(_frameNumber / 120D)), 0)
        );

        var image = (VulkanImage)targetTexture;

        // Transition image to transfer destination for the clear operation
        image.TransitionLayout(cmd, ImageLayout.TransferDstOptimal, AccessFlags.TransferWriteBit);

        // Clear the image with the animated color
        var clearColor = clearValue.Color;
        var range = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1);
        context.Api.CmdClearColorImage(
            cmd,
            image,
            ImageLayout.TransferDstOptimal,
            &clearColor,
            1,
            &range
        );

        // Transition to General for external sharing / composition
        image.TransitionLayout(cmd, ImageLayout.General, AccessFlags.MemoryReadBit);

        cmd.Submit();

        _frameNumber++;
        _timings.Add(provider.GetElapsedTime(startTimestamp));

        return _timings.ComputeStats();
    }

    // public unsafe void UploadMesh(ref RenderMesh renderMesh)
    // {
    //     ref var mesh = ref renderMesh.Mesh;
    //
    //     var bufferInfo = new BufferCreateInfo(
    //         size: mesh.Vertices.SizeOf(),
    //         usage: BufferUsageFlags.VertexBufferBit
    //     );
    //     var allocInfo = new AllocationCreateInfo(usage: MemoryUsage.CPU_To_GPU);
    //
    //     renderMesh.RenderBuffer = new VulkanBuffer(
    //         _ctx.Allocator,
    //         bufferInfo,
    //         allocInfo
    //     ).DisposeWith(_bufferDisposable);
    //
    //     using var mapping = renderMesh.RenderBuffer.Map<Vertex>();
    //     if (!mapping.TryGetSpan(out var span))
    //         throw new AllocationException("Couldn't get vertices span from allocation");
    //
    //     mesh.Vertices.ToArray().AsSpan().CopyTo(span);
    // }

    private unsafe void CreateRenderPass()
    {
        // var colorAttachment = new AttachmentDescription(
        //     format: _swapchain!.ImageFormat,
        //     samples: SampleCountFlags.Count1Bit,
        //     loadOp: AttachmentLoadOp.Clear,
        //     storeOp: AttachmentStoreOp.Store,
        //     stencilLoadOp: AttachmentLoadOp.DontCare,
        //     stencilStoreOp: AttachmentStoreOp.DontCare,
        //     initialLayout: ImageLayout.Undefined,
        //     finalLayout: ImageLayout.PresentSrcKhr
        // );
        //
        // var colorAttachmentRef = new AttachmentReference(0, ImageLayout.AttachmentOptimal);
        //
        // var depthAttachment = new AttachmentDescription(
        //     format: DepthFormat,
        //     samples: SampleCountFlags.Count1Bit,
        //     loadOp: AttachmentLoadOp.Clear,
        //     storeOp: AttachmentStoreOp.Store,
        //     stencilLoadOp: AttachmentLoadOp.Clear,
        //     stencilStoreOp: AttachmentStoreOp.DontCare,
        //     initialLayout: ImageLayout.Undefined,
        //     finalLayout: ImageLayout.DepthStencilAttachmentOptimal
        // );
        // var depthAttachmentRef = new AttachmentReference(
        //     attachment: 1,
        //     ImageLayout.DepthStencilAttachmentOptimal
        // );
        //
        // var subpass = new SubpassDescription(
        //     pipelineBindPoint: PipelineBindPoint.Graphics,
        //     colorAttachmentCount: 1,
        //     pColorAttachments: &colorAttachmentRef,
        //     pDepthStencilAttachment: &depthAttachmentRef
        // );
        //
        // var dependency = new SubpassDependency(
        //     Vk.SubpassExternal,
        //     0,
        //     PipelineStageFlags.ColorAttachmentOutputBit,
        //     PipelineStageFlags.ColorAttachmentOutputBit,
        //     0,
        //     AccessFlags.ColorAttachmentWriteBit
        // );
        // var depthDependency = new SubpassDependency(
        //     Vk.SubpassExternal,
        //     0,
        //     PipelineStageFlags.EarlyFragmentTestsBit | PipelineStageFlags.LateFragmentTestsBit,
        //     PipelineStageFlags.EarlyFragmentTestsBit | PipelineStageFlags.LateFragmentTestsBit,
        //     0,
        //     AccessFlags.DepthStencilAttachmentWriteBit
        // );
        //
        // var attachments = new[] { colorAttachment, depthAttachment };
        // var dependencies = new[] { dependency, depthDependency };
        //
        // var renderPassInfo = new RenderPassCreateInfo(
        //     attachmentCount: 2,
        //     pAttachments: attachments.AsPtr(),
        //     subpassCount: 1,
        //     pSubpasses: &subpass,
        //     dependencyCount: (uint)dependencies.Length,
        //     pDependencies: dependencies.AsPtr()
        // );
        //
        // _renderPass = new(
        //     _ctx.Api,
        //     _ctx.Device,
        //     _ctx.Callbacks.WithUserData("RenderPass"),
        //     renderPassInfo
        // );
    }
}
