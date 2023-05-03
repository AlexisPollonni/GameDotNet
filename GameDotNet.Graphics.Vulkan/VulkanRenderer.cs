using System.Numerics;
using Arch.Core;
using Arch.Core.Extensions;
using GameDotNet.Core.Physics.Components;
using GameDotNet.Core.Tools.Containers;
using GameDotNet.Core.Tools.Extensions;
using GameDotNet.Graphics.Vulkan.Bootstrap;
using GameDotNet.Graphics.Vulkan.MemoryAllocation;
using GameDotNet.Graphics.Vulkan.Tools.Extensions;
using GameDotNet.Graphics.Vulkan.Wrappers;
using Microsoft.Toolkit.HighPerformance;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using static GameDotNet.Graphics.Shaders.Shaders;

namespace GameDotNet.Graphics.Vulkan;

public sealed class VulkanRenderer : IDisposable
{
    private ulong _frameNumber;
    private readonly DisposableList _bufferDisposable;

    private readonly DefaultVulkanContext _ctx;
    private VulkanSwapchain? _swapchain;
    private VulkanPipeline _meshPipeline = null!;

    private VulkanShader _meshFragShader = null!;
    private VulkanShader _meshVertShader = null!;

    private VulkanFramebuffer[] _frameBuffers;
    private VulkanRenderPass _renderPass;
    private VulkanSemaphore _presentSemaphore, _renderSemaphore;
    private VulkanImage? _depthImage;
    private VulkanImageView? _depthImageView;
    private VulkanFence? _renderFence;
    private const Format DepthFormat = Format.D32Sfloat;

    public VulkanRenderer(DefaultVulkanContext context)
    {
        _ctx = context;
        _frameBuffers = Array.Empty<VulkanFramebuffer>();
        _bufferDisposable = new();
    }

    public void Dispose()
    {
        _meshVertShader.Dispose();
        _meshVertShader.Dispose();
        _bufferDisposable.Dispose();
        _renderPass.Dispose();
        _frameBuffers.DisposeAll();

        _meshPipeline.Dispose();

        // Order is important
        _depthImage?.Dispose();
        _depthImageView?.Dispose();
        _swapchain?.Dispose();
    }

    public void Initialize()
    {
        CreateSwapchain();

        _meshFragShader = new(_ctx.Api, _ctx.Device, ShaderStageFlags.FragmentBit, MeshFragmentShader);
        _meshVertShader = new(_ctx.Api, _ctx.Device, ShaderStageFlags.VertexBit, MeshVertexShader);

        CreateRenderPass();
        CreateFrameBuffers();
        CreateSyncStructures();
        CreatePipeline();
    }

    public unsafe void Draw(TimeSpan dt, in QueryChunkIterator chunks, in Entity camera)
    {
        var vk = _ctx.Api;
        // wait until the GPU has finished rendering the last frame. Timeout of 1 second
        _renderFence?.Wait(1000000000);

        var res = _swapchain!.AcquireNextImage(1000000000, _presentSemaphore, null, out var swImgIndex);
        if (res is Result.ErrorOutOfDateKhr or Result.SuboptimalKhr)
        {
            RecreateSwapChain();
            return;
        }

        if (res is not Result.Success)
        {
            res.LogError("Failed to acquire swapchain Image");
            return;
        }

        using var cmd = _ctx.Pool.CreateCommandBuffer(_renderFence);

        cmd.BeginRecording();

        // make a clear-color from frame number. This will flash with a 120*pi frame period.
        var clearValue = new ClearValue(new(0, 0, (float)Math.Abs(Math.Sin(_frameNumber / 120D)), 0));
        var depthClear = new ClearValue(depthStencil: new(1f));
        ReadOnlySpan<ClearValue> clearValues = stackalloc[] { clearValue, depthClear };

        _renderPass.Begin(cmd, new(null, _swapchain.Extent), _frameBuffers[swImgIndex], clearValues);

        // RENDERING COMMANDS
        vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _meshPipeline);

        // camera position
        var camPos = camera.Get<Translation>();
        var camRot = camera.Get<Rotation>();

        var view = Transform.ToMatrix(Vector3.One, camRot, camPos);
        Matrix4x4.Invert(view, out view);


        var projection = Matrix4x4.CreatePerspectiveFieldOfView(Scalar.DegreesToRadians(70f),
                                                                (float)_swapchain.Extent.Width /
                                                                _swapchain.Extent.Height,
                                                                0.1f, 5000f);
        // Invert Y axis as vulkan points downwards
        projection.M22 *= -1;

        var size = (uint)sizeof((Vector4, Matrix4x4));

        foreach (ref var chunk in chunks)
        {
            foreach (var index in chunk)
            {
                ref var render = ref chunk.Get<RenderMesh>(index);
                vk.CmdBindVertexBuffers(cmd, 0, 1, render.RenderBuffer!.Buffer, 0);

                var e = chunk.Entities[index];

                if (!e.TryGet<Scale>(out var scale)) scale = new();
                if (!e.TryGet<Rotation>(out var rot)) rot = new();
                if (!e.TryGet<Translation>(out var translation)) translation = new();

                //model rotation
                var model = Transform.ToMatrix(scale, rot, translation);


                var meshMatrix = model * view * projection;
                var constants = (Vector4.Zero, meshMatrix);


                vk.CmdPushConstants(cmd, _meshPipeline.Layout, ShaderStageFlags.VertexBit, 0, size,
                                    ref constants);

                vk.CmdDraw(cmd, (uint)render.Mesh.Vertices.Count, 1, 0, 0);
            }
        }

        _renderPass.End(cmd);

        // prepare the submission to the queue.
        // we want to wait on the _presentSemaphore, as that semaphore is signaled when the swapchain is ready
        // we will signal the _renderSemaphore, to signal that rendering has finished
        const PipelineStageFlags waitStage = PipelineStageFlags.ColorAttachmentOutputBit;

        // submit command buffer to the queue and execute it.
        // _renderFence will now block until the graphic commands finish execution
        cmd.Submit(_presentSemaphore, waitStage, _renderSemaphore);


        // this will put the image we just rendered into the visible window.
        // we want to wait on the _renderSemaphore for that,
        // as it's necessary that drawing commands have finished before the image is displayed to the user
        res = _swapchain.QueuePresent(_ctx.MainGraphicsQueue, _renderSemaphore, swImgIndex);
        if (res is Result.ErrorOutOfDateKhr or Result.SuboptimalKhr)
        {
            return;
        }

        if (res is not Result.Success)
        {
            res.LogError("Failed to present swapchain image");
            return;
        }

        _frameNumber++;
    }

    public unsafe void UploadMesh(ref RenderMesh renderMesh)
    {
        ref var mesh = ref renderMesh.Mesh;

        var bufferInfo = new BufferCreateInfo(size: mesh.Vertices.SizeOf(), usage: BufferUsageFlags.VertexBufferBit);
        var allocInfo = new AllocationCreateInfo(usage: MemoryUsage.CPU_To_GPU);

        renderMesh.RenderBuffer = new VulkanBuffer(_ctx.Allocator, bufferInfo, allocInfo)
            .DisposeWith(_bufferDisposable);

        using var mapping = renderMesh.RenderBuffer.Map<Vertex>();
        if (!mapping.TryGetSpan(out var span))
            throw new AllocationException("Couldn't get vertices span from allocation");

        mesh.Vertices.AsSpan().CopyTo(span);
    }

    private bool CreateSwapchain()
    {
        var swapchain = new SwapchainBuilder(_ctx.Instance, _ctx.PhysDevice, _ctx.Device,
                                             new()
                                             {
                                                 Surface = _ctx.Surface,
                                                 DesiredPresentModes = new() { PresentModeKHR.FifoKhr },
                                                 OldSwapchain = _swapchain
                                             })
            .Build();

        _swapchain?.Dispose();
        _depthImage?.Dispose();
        _depthImageView?.Dispose();
        _swapchain = swapchain;

        if (swapchain.Extent.Height is 0 || swapchain.Extent.Width is 0) return false;

        var depthImageExtent = new Extent3D(swapchain.Extent.Width, swapchain.Extent.Height, 1);
        var depthImgInfo =
            VulkanImage.GetImageCreateInfo(DepthFormat, ImageUsageFlags.DepthStencilAttachmentBit, depthImageExtent);

        var allocInfo =
            new AllocationCreateInfo(usage: MemoryUsage.GPU_Only, requiredFlags: MemoryPropertyFlags.DeviceLocalBit);

        _depthImage = new(_ctx.Api, _ctx.Device, _ctx.Allocator, depthImgInfo, allocInfo);
        _depthImageView = _depthImage.GetImageView(DepthFormat, ImageAspectFlags.DepthBit);

        return true;
    }

    private unsafe void CreateRenderPass()
    {
        var colorAttachment = new AttachmentDescription(format: _swapchain!.ImageFormat,
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

        _renderPass = new(_ctx.Api, _ctx.Device, _ctx.Callbacks.WithUserData("RenderPass"), renderPassInfo);
    }

    private unsafe void CreateFrameBuffers()
    {
        _frameBuffers = _swapchain!.GetImageViews().Select(view =>
        {
            ReadOnlySpan<ImageView> attach = stackalloc[] { view, _depthImageView!.ImageView };
            return new VulkanFramebuffer(_ctx, _swapchain.Extent, _renderPass, attach);
        }).ToArray();
    }

    private void CreateSyncStructures()
    {
        _renderFence = new(_ctx.Api, _ctx.Device, FenceCreateFlags.SignaledBit, _ctx.Callbacks);

        _presentSemaphore = new(_ctx.Api, _ctx.Device, _ctx.Callbacks);
        _renderSemaphore = new(_ctx.Api, _ctx.Device, _ctx.Callbacks);
    }

    private void CreatePipeline()
    {
        _meshPipeline = new PipelineBuilder(_ctx.Instance, _ctx.Device)
            .Build(new()
            {
                VertexInputDescription = _meshVertShader.GetVertexDescription(),
                ShaderStages = new[] { _meshFragShader, _meshVertShader },
                RenderPass = _renderPass,
                Viewport = new(0, 0, _swapchain?.Extent.Width, _swapchain?.Extent.Height, 0, 1f),
                Scissor = new(new(0, 0), _swapchain?.Extent),
                EnableDepthTest = true, EnableDepthWrite = true, DepthStencilCompare = CompareOp.LessOrEqual
            });
    }

    private void RecreateSwapChain()
    {
        var vk = _ctx.Api;
        vk.DeviceWaitIdle(_ctx.Device);

        _frameBuffers.DisposeAll();
        _meshPipeline.Dispose();

        if (!CreateSwapchain()) return;
        CreateFrameBuffers();
        CreatePipeline();
    }
}