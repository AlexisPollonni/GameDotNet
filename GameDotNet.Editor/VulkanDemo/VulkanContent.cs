using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
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
    private ulong _frameNber;

    private readonly AvaloniaVulkanContext _context;
    private readonly VulkanShader _vertShader, _fragShader;
    private RenderPass _renderPass;
    private VulkanPipeline _pipeline;

    private Framebuffer[] _framebuffers;
    private VulkanImage _depthImage;
    private VulkanImageView _depthImageView;

    private ISwapchain? _previousSwapchain;
    private bool _isInit;

    public VulkanContent(AvaloniaVulkanContext context)
    {
        _context = context;
        _vertShader = new(_context.Api, _context.Device, ShaderStageFlags.VertexBit, Shaders.MeshVertexShader);
        _fragShader = new(_context.Api, _context.Device, ShaderStageFlags.FragmentBit, Shaders.MeshFragmentShader);
    }

    public unsafe void Render(ISwapchain swapchain)
    {
        var curImage = swapchain.GetCurrentImage();

        var api = _context.Api;
        _context.Pool.FreeUsedCommandBuffers();


        if (!Equals(swapchain, _previousSwapchain))
            RecreateTemporalObjects(swapchain);

        _previousSwapchain = swapchain;

        var cmd = _context.Pool.CreateCommandBuffer();
        cmd.BeginRecording();


        // make a clear-color from frame number. This will flash with a 120*pi frame period.
        var clearValue = new ClearValue(new(0, 0, (float)Math.Abs(Math.Sin(_frameNber / 120D)), 0));
        var depthClear = new ClearValue(depthStencil: new(1f));
        var clearValues = new[] { clearValue, depthClear };

        var rpInfo = new RenderPassBeginInfo(renderPass: _renderPass,
                                             renderArea: new Rect2D(new Offset2D(0, 0),
                                                                    new((uint)curImage.Size.Width,
                                                                        (uint)curImage.Size.Height)),
                                             framebuffer: _framebuffers[swapchain.CurrentImageIndex],
                                             clearValueCount: (uint)clearValues.Length,
                                             pClearValues: clearValues.AsPtr());

        api.CmdBeginRenderPass(cmd, rpInfo, SubpassContents.Inline);

        api.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _pipeline);


        api.CmdEndRenderPass(cmd);


        cmd.Submit();

        _frameNber++;
    }

    public void Dispose()
    {
        DestroyTemporalObjects();

        _vertShader.Dispose();
        _fragShader.Dispose();
    }

    private void RecreateTemporalObjects(ISwapchain swapchain)
    {
        DestroyTemporalObjects();

        var cur = swapchain.GetCurrentImage();

        CreateImages(cur.Size);
        CreateRenderPass(cur.Format, cur.Layout);
        CreateFrameBuffers(swapchain.GetImageList());
        CreatePipeline(cur.Size);

        _isInit = true;
    }

    private unsafe void DestroyTemporalObjects()
    {
        var vk = _context.Api;
        vk.DeviceWaitIdle(_context.Device);

        if (!_isInit) return;

        _depthImageView.Dispose();
        _depthImage.Dispose();

        foreach (var framebuffer in _framebuffers)
            vk.DestroyFramebuffer(_context.Device, framebuffer, null);

        _pipeline.Dispose();
        vk.DestroyRenderPass(_context.Device, _renderPass, null);
    }

    private void CreateImages(in Size size)
    {
        var depthImgInfo = VulkanImage.GetImageCreateInfo(DepthFormat, ImageUsageFlags.DepthStencilAttachmentBit,
                                                          new((uint)size.Width, (uint)size.Height, 1));
        var allocInfo =
            new AllocationCreateInfo(usage: MemoryUsage.GPU_Only, requiredFlags: MemoryPropertyFlags.DeviceLocalBit);
        _depthImage = new(_context.Api, _context.Device, _context.Allocator, depthImgInfo, allocInfo);
        _depthImageView = _depthImage.GetImageView(DepthFormat, ImageAspectFlags.DepthBit);
    }

    private unsafe void CreateRenderPass(Format format, ImageLayout layout)
    {
        var colorAttachment = new AttachmentDescription(format: format,
                                                        samples: SampleCountFlags.Count1Bit,
                                                        loadOp: AttachmentLoadOp.Clear,
                                                        storeOp: AttachmentStoreOp.Store,
                                                        stencilLoadOp: AttachmentLoadOp.DontCare,
                                                        stencilStoreOp: AttachmentStoreOp.DontCare,
                                                        initialLayout: layout,
                                                        finalLayout: layout);

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

    private unsafe void CreateFrameBuffers(IReadOnlyList<SwapchainImage> images)
    {
        _framebuffers = new Framebuffer[images.Count];

        Span<ImageView> fbAttachments = stackalloc[] { images[0].ViewHandle, _depthImageView.ImageView };

        fixed (ImageView* pAttach = fbAttachments)
            for (var i = 0; i < images.Count; i++)
            {
                fbAttachments[0] = images[i].ViewHandle;
                var fbInfo = new FramebufferCreateInfo(renderPass: _renderPass,
                                                       width: (uint)images[i].Size.Width,
                                                       height: (uint)images[i].Size.Height,
                                                       attachmentCount: (uint)fbAttachments.Length,
                                                       pAttachments: pAttach, layers: 1);

                _context.Api.CreateFramebuffer(_context.Device, fbInfo, null, out _framebuffers[i]).ThrowOnError();
            }
    }

    private void CreatePipeline(in Size size)
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