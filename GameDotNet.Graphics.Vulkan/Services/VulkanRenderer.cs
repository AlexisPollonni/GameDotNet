using System.Drawing;
using GameDotNet.Core.Tooling.Extensions;
using GameDotNet.Graphics.Abstractions;
using GameDotNet.Graphics.Tooling;
using GameDotNet.Graphics.Vulkan.Abstractions;
using GameDotNet.Graphics.Vulkan.Tools.Extensions;
using GameDotNet.Graphics.Vulkan.Wrappers;
using Microsoft.Extensions.DependencyInjection;
using Silk.NET.Vulkan;
using ZLinq;

namespace GameDotNet.Graphics.Vulkan.Services;

public sealed class VulkanRenderer(
    IVulkanContext context,
    TimeProvider provider,
    [FromKeyedServices(QueueFlags.GraphicsBit)] CommandSubmitter submitter
) : IEntityRenderer
{
    private ulong _frameNumber;
    private const Format DepthFormat = Format.D32Sfloat;
    private readonly TimingsRingBuffer _timings = new(100);

    public async ValueTask<TimelineStats> Render(IDeviceTexture targetTexture)
    {
        var startTimestamp = provider.GetTimestamp();

        // make a clear-color from frame number. This will flash with a 120*pi frame period.
        var currentColor = Color.FromArgb(
            (int)(Math.Abs(Math.Sin(_frameNumber / 120D)) * 255),
            Color.Cyan
        );
        var clearValue = currentColor.ToClearColor();

        var image = (VulkanImage)targetTexture;
        var batch = submitter.CreateBatch();

        using (var recorder = submitter.CreateRecorder(batch))
        {
            var cmd = recorder.Buffer;

            // Transition image to transfer destination for the clear operation
            recorder.TransitionLayout(
                image,
                ImageLayout.TransferDstOptimal,
                AccessFlags.TransferWriteBit
            );

            // Clear the image with the animated color
            var clearColor = clearValue.Color;
            var range = new ImageSubresourceRange(ImageAspectFlags.ColorBit, 0, 1, 0, 1);
            context.Api.CmdClearColorImage(
                cmd,
                image,
                ImageLayout.TransferDstOptimal,
                in clearColor,
                [range]
            );
            // Transition to shader for external sharing / composition
            recorder.TransitionLayout(
                image,
                ImageLayout.ShaderReadOnlyOptimal,
                AccessFlags.ShaderReadBit
            );
        }

        await submitter.SubmitAsync(batch);

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

    private unsafe void BeginRendering(
        VulkanCommandBuffer buffer,
        Color clearColor,
        Rectangle area,
        ReadOnlySpan<VulkanImageView> colors,
        VulkanImageView? depth = null,
        VulkanImageView? stencil = null
    )
    {
        Span<RenderingAttachmentInfo> colorAttachments =
            stackalloc RenderingAttachmentInfo[colors.Length];
        ClearValue? clearColorValue = clearColor.IsEmpty ? null : clearColor.ToClearColor();
        ClearValue depthClearValue = new(depthStencil: new ClearDepthStencilValue(1.0f, 0));

        colors
            .AsValueEnumerable()
            .WithState(clearColorValue)
            .Select(pair => new RenderingAttachmentInfo(
                imageLayout: ImageLayout.ColorAttachmentOptimal,
                loadOp: pair.state is null ? AttachmentLoadOp.Load : AttachmentLoadOp.Clear,
                storeOp: AttachmentStoreOp.Store,
                clearValue: pair.state,
                imageView: pair.item.ImageView
            ))
            .CopyTo(colorAttachments);

        RenderingAttachmentInfo depthAttachment =
                new(
                    imageLayout: ImageLayout.DepthStencilAttachmentOptimal,
                    loadOp: AttachmentLoadOp.Clear,
                    storeOp: AttachmentStoreOp.DontCare,
                    clearValue: depthClearValue,
                    imageView: depth?.ImageView ?? default //default disables the attachment
                ),
            stencilAttachment =
                new(
                    imageLayout: ImageLayout.DepthAttachmentOptimal,
                    loadOp: AttachmentLoadOp.Clear,
                    storeOp: AttachmentStoreOp.DontCare,
                    clearValue: depthClearValue,
                    imageView: stencil?.ImageView ?? default
                );

        fixed (RenderingAttachmentInfo* pColorAttachments = colorAttachments)
        {
            var renderInfo = new RenderingInfo(
                flags: RenderingFlags.ContentsSecondaryCommandBuffersBit,
                renderArea: new(
                    extent: new((uint)area.Width, (uint)area.Height),
                    offset: new(area.X, area.Y)
                ),
                layerCount: 1,
                viewMask: 0,
                pColorAttachments: pColorAttachments,
                colorAttachmentCount: (uint)colors.Length,
                pDepthAttachment: &depthAttachment,
                pStencilAttachment: &stencilAttachment
            );

            context.Api.CmdBeginRendering(buffer, in renderInfo);
        }
    }
}
