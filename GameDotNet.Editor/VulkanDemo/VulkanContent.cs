using System;
using System.Diagnostics;
using Avalonia;
using GameDotNet.Core.Tools.Extensions;
using GameDotNet.Graphics.Shaders;
using GameDotNet.Graphics.Vulkan.Bootstrap;
using GameDotNet.Graphics.Vulkan.MemoryAllocation;
using GameDotNet.Graphics.Vulkan.Tools.Extensions;
using GameDotNet.Graphics.Vulkan.Wrappers;
using Silk.NET.Vulkan;

namespace GameDotNet.Editor.VulkanDemo;

internal class VulkanContent : IDisposable
{
    private const Format DepthFormat = Format.D32Sfloat;
    private readonly Stopwatch St = Stopwatch.StartNew();
    private ulong _frameNber = 0;

    private readonly VulkanContext _context;
    private readonly VulkanShader _vertShader, _fragShader;
    private RenderPass _renderPass;
    private VulkanPipeline _pipeline;

    private Framebuffer _framebuffer;
    private VulkanImage _colorAttachment;
    private VulkanImageView _colorImageView;
    private VulkanImage _depthImage;
    private VulkanImageView _depthImageView;

    private PixelSize _previousImageSize = PixelSize.Empty;
    private bool _isInit;

    public VulkanContent(VulkanContext context)
    {
        _context = context;
        _vertShader = new(_context.Api, _context.Device, ShaderStageFlags.VertexBit, Shaders.MeshVertexShader);
        _fragShader = new(_context.Api, _context.Device, ShaderStageFlags.FragmentBit, Shaders.MeshFragmentShader);
    }

    public unsafe void Render(VulkanImage image)
    {
        var api = _context.Api;

        var size = new PixelSize((int)image.Extent.Width, (int)image.Extent.Height);
        if (size != _previousImageSize)
            RecreateTemporalObjects(size);

        _previousImageSize = size;

        var cmd = _context.Pool.CreateCommandBuffer();
        cmd.BeginRecording();

        _colorAttachment.TransitionLayout(cmd, ImageLayout.Undefined, AccessFlags.None,
                                          ImageLayout.ColorAttachmentOptimal, AccessFlags.ColorAttachmentWriteBit);

        // make a clear-color from frame number. This will flash with a 120*pi frame period.
        var clearValue = new ClearValue(new(0, 0, (float)Math.Abs(Math.Sin(_frameNber / 120D)), 0));
        var depthClear = new ClearValue(depthStencil: new(1f));
        var clearValues = new[] { clearValue, depthClear };

        var rpInfo = new RenderPassBeginInfo(renderPass: _renderPass,
                                             renderArea: new Rect2D(new Offset2D(0, 0),
                                                                    new(_colorAttachment.Extent.Width,
                                                                        _colorAttachment.Extent.Height)),
                                             framebuffer: _framebuffer,
                                             clearValueCount: (uint)clearValues.Length,
                                             pClearValues: clearValues.AsPtr());

        api.CmdBeginRenderPass(cmd, rpInfo, SubpassContents.Inline);

        api.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _pipeline);


        api.CmdEndRenderPass(cmd);

        _colorAttachment.TransitionLayout(cmd, ImageLayout.TransferSrcOptimal, AccessFlags.TransferReadBit);
        image.TransitionLayout(cmd, ImageLayout.TransferDstOptimal, AccessFlags.TransferWriteBit);

        var elem1 = new Offset3D((int)image.Extent.Width, (int)image.Extent.Height, 1);
        var srcBlitRegion = new ImageBlit
        {
            SrcOffsets = new()
            {
                Element0 = new(0, 0, 0),
                Element1 = elem1
            },
            DstOffsets = new()
            {
                Element0 = new(0, 0, 0),
                Element1 = elem1
            },
            SrcSubresource = new(ImageAspectFlags.ColorBit, 0, 0, 1),
            DstSubresource = new(ImageAspectFlags.ColorBit, 0, 0, 1)
        };

        api.CmdBlitImage(cmd, _colorAttachment, ImageLayout.TransferSrcOptimal,
                         image, ImageLayout.TransferDstOptimal,
                         1, srcBlitRegion, Filter.Linear);

        cmd.Submit();

        _frameNber++;
    }

    public void Dispose()
    {
        DestroyTemporalObjects();

        _vertShader.Dispose();
        _fragShader.Dispose();
    }

    private void RecreateTemporalObjects(in PixelSize size)
    {
        DestroyTemporalObjects();

        CreateImages(size);
        CreateRenderPass();
        CreateFrameBuffer(size);
        CreatePipeline(size);

        _isInit = true;
    }

    private unsafe void DestroyTemporalObjects()
    {
        var vk = _context.Api;
        vk.DeviceWaitIdle(_context.Device);

        if (!_isInit) return;

        _depthImageView.Dispose();
        _depthImage.Dispose();

        vk.DestroyFramebuffer(_context.Device, _framebuffer, null);
        _pipeline.Dispose();
        vk.DestroyRenderPass(_context.Device, _renderPass, null);

        _colorImageView.Dispose();
        _colorAttachment.Dispose();
    }

    private void CreateImages(in PixelSize size)
    {
        _colorAttachment = _context.CreateVulkanImage((uint)Format.R8G8B8A8Unorm, size, false).image;
        _colorImageView = _colorAttachment.GetImageView(_colorAttachment.Format, ImageAspectFlags.ColorBit);
        _colorAttachment.TransitionLayout(_context.Pool, ImageLayout.ColorAttachmentOptimal, AccessFlags.NoneKhr);

        var depthImgInfo = VulkanImage.GetImageCreateInfo(DepthFormat, ImageUsageFlags.DepthStencilAttachmentBit,
                                                          new((uint)size.Width, (uint)size.Height, 1));
        var allocInfo =
            new AllocationCreateInfo(usage: MemoryUsage.GPU_Only, requiredFlags: MemoryPropertyFlags.DeviceLocalBit);
        _depthImage = new(_context.Api, _context.Device, _context.Allocator, depthImgInfo, allocInfo);
        _depthImageView = _depthImage.GetImageView(DepthFormat, ImageAspectFlags.DepthBit);
    }

    private unsafe void CreateRenderPass()
    {
        var colorAttachment = new AttachmentDescription(format: _colorAttachment.Format,
                                                        samples: SampleCountFlags.Count1Bit,
                                                        loadOp: AttachmentLoadOp.Clear,
                                                        storeOp: AttachmentStoreOp.Store,
                                                        stencilLoadOp: AttachmentLoadOp.DontCare,
                                                        stencilStoreOp: AttachmentStoreOp.DontCare,
                                                        initialLayout: ImageLayout.Undefined,
                                                        finalLayout: ImageLayout.PresentSrcKhr);

        var colorAttachmentRef = new AttachmentReference(0, ImageLayout.AttachmentOptimal);

        var depthAttachment = new AttachmentDescription(format: DepthFormat,
                                                        samples: SampleCountFlags.Count1Bit,
                                                        loadOp: AttachmentLoadOp.Clear,
                                                        storeOp: AttachmentStoreOp.Store,
                                                        stencilLoadOp: AttachmentLoadOp.Clear,
                                                        stencilStoreOp: AttachmentStoreOp.DontCare,
                                                        initialLayout: ImageLayout.Undefined,
                                                        finalLayout: ImageLayout.DepthStencilAttachmentOptimal);
        var depthAttachmentRef = new AttachmentReference(attachment: 1, ImageLayout.DepthStencilAttachmentOptimal);

        var subpass = new SubpassDescription(pipelineBindPoint: PipelineBindPoint.Graphics,
                                             colorAttachmentCount: 1, pColorAttachments: &colorAttachmentRef,
                                             pDepthStencilAttachment: &depthAttachmentRef);


        var dependency = new SubpassDependency(Vk.SubpassExternal, 0, PipelineStageFlags.ColorAttachmentOutputBit,
                                               PipelineStageFlags.ColorAttachmentOutputBit, 0,
                                               AccessFlags.ColorAttachmentWriteBit);
        var depthDependency = new SubpassDependency(Vk.SubpassExternal, 0,
                                                    PipelineStageFlags.EarlyFragmentTestsBit |
                                                    PipelineStageFlags.LateFragmentTestsBit,
                                                    PipelineStageFlags.EarlyFragmentTestsBit |
                                                    PipelineStageFlags.LateFragmentTestsBit, 0,
                                                    AccessFlags.DepthStencilAttachmentWriteBit);


        var attachments = new[] { colorAttachment, depthAttachment };
        var dependencies = new[] { dependency, depthDependency };

        var renderPassInfo = new RenderPassCreateInfo(attachmentCount: 2,
                                                      pAttachments: attachments.AsPtr(),
                                                      subpassCount: 1, pSubpasses: &subpass,
                                                      dependencyCount: (uint)dependencies.Length,
                                                      pDependencies: dependencies.AsPtr());

        _context.Api.CreateRenderPass(_context.Device, renderPassInfo, null, out _renderPass);
    }

    private unsafe void CreateFrameBuffer(in PixelSize size)
    {
        var fbAttachments = new[] { _colorImageView.ImageView, _depthImageView.ImageView };
        var fbInfo = new FramebufferCreateInfo(renderPass: _renderPass,
                                               width: (uint)size.Width, height: (uint)size.Height,
                                               attachmentCount: (uint)fbAttachments.Length,
                                               pAttachments: fbAttachments.AsPtr(), layers: 1);

        _context.Api.CreateFramebuffer(_context.Device, fbInfo, null, out _framebuffer).ThrowOnError();
    }

    private void CreatePipeline(in PixelSize size)
    {
        _pipeline = new PipelineBuilder(_context.Instance, _context.Device)
            .Build(new()
            {
                VertexInputDescription = _vertShader.GetVertexDescription(),
                ShaderStages = new[] { _vertShader, _fragShader },
                RenderPass = _renderPass,
                Viewport = new(0, 0, size.Width, size.Height, 0f, 1f),
                Scissor = new(new(), new((uint)size.Width, (uint)size.Height)),
                EnableDepthTest = true, EnableDepthWrite = true, DepthStencilCompare = CompareOp.LessOrEqual
            });
    }
}