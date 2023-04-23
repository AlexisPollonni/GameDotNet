using System.Diagnostics;
using System.Numerics;
using Arch.Core;
using Arch.Core.Extensions;
using GameDotNet.Core.Physics.Components;
using GameDotNet.Core.Tools.Containers;
using GameDotNet.Core.Tools.Extensions;
using GameDotNet.Graphics.Vulkan.Bootstrap;
using GameDotNet.Graphics.Vulkan.MemoryAllocation;
using GameDotNet.Graphics.Vulkan.Tools;
using GameDotNet.Graphics.Vulkan.Tools.Extensions;
using GameDotNet.Graphics.Vulkan.Wrappers;
using Microsoft.Toolkit.HighPerformance;
using Silk.NET.Core;
using Silk.NET.Core.Contexts;
using Silk.NET.Core.Native;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using static GameDotNet.Graphics.Vulkan.Tools.Constants;
using static GameDotNet.Graphics.Shaders.Shaders;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace GameDotNet.Graphics.Vulkan;

public sealed class VulkanRenderer : IDisposable
{
    private ulong _frameNumber;
    private readonly IView _window;
    private readonly DisposableList _bufferDisposable;

    private VulkanInstance _instance = null!;
    private VulkanDevice _device = null!;
    private VulkanPhysDevice _physDevice = null!;
    private VulkanSurface _surface = null!;
    private VulkanSwapchain? _swapchain;
    private VulkanPipeline _meshPipeline = null!;
    private VulkanMemoryAllocator _allocator = null!;

    private VulkanShader _meshFragShader = null!;
    private VulkanShader _meshVertShader = null!;

    private Queue _graphicsQueue;
    private Framebuffer[] _frameBuffers;
    private CommandPool _commandPool;
    private CommandBuffer _mainCommandBuffer;
    private RenderPass _renderPass;
    private Semaphore _presentSemaphore, _renderSemaphore;
    private Fence _renderFence;
    private VulkanImage? _depthImage;
    private VulkanImageView? _depthImageView;
    private const Format DepthFormat = Format.D32Sfloat;

    public VulkanRenderer(IView window)
    {
        _window = window;
        _frameBuffers = Array.Empty<Framebuffer>();
        _bufferDisposable = new();
    }

    public void Dispose()
    {
        _meshVertShader.Dispose();
        _meshVertShader.Dispose();
        _bufferDisposable.Dispose();
        _instance.Vk.DestroyRenderPass(_device, _renderPass, NullAlloc);
        foreach (var framebuffer in _frameBuffers)
        {
            _instance.Vk.DestroyFramebuffer(_device, framebuffer, NullAlloc);
        }

        _instance.Vk.DestroyCommandPool(_device, _commandPool, NullAlloc);

        _meshPipeline.Dispose();

        // Order is important
        _depthImage?.Dispose();
        _depthImageView?.Dispose();
        _swapchain?.Dispose();

        _allocator.Dispose();
        _device.Dispose();
        _surface.Dispose();
        _instance.Dispose();
    }

    public void Initialize()
    {
        InitVulkan();

        _meshFragShader = new(_instance.Vk, _device, ShaderStageFlags.FragmentBit, MeshFragmentShader);
        _meshVertShader = new(_instance.Vk, _device, ShaderStageFlags.VertexBit, MeshVertexShader);

        CreateCommands();
        CreateRenderPass();
        CreateFrameBuffers();
        CreateSyncStructures();
        CreatePipeline();
    }

    public unsafe void Draw(TimeSpan dt, in QueryChunkIterator chunks, in Entity camera)
    {
        var vk = _instance.Vk;
        // wait until the GPU has finished rendering the last frame. Timeout of 1 second
        vk.WaitForFences(_device, 1, _renderFence, true, 1000000000);

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

        vk.ResetFences(_device, 1, _renderFence);
        vk.ResetCommandBuffer(_mainCommandBuffer, 0);

        var cmdBeginInfo =
            new CommandBufferBeginInfo(flags: CommandBufferUsageFlags.OneTimeSubmitBit);

        vk.BeginCommandBuffer(_mainCommandBuffer, cmdBeginInfo);

        // make a clear-color from frame number. This will flash with a 120*pi frame period.
        var clearValue = new ClearValue(new(0, 0, (float)Math.Abs(Math.Sin(_frameNumber / 120D)), 0));
        var depthClear = new ClearValue(depthStencil: new(1f));
        var clearValues = new[] { clearValue, depthClear };

        var rpInfo = new RenderPassBeginInfo(renderPass: _renderPass,
                                             renderArea: new Rect2D(new Offset2D(0, 0), _swapchain.Extent),
                                             framebuffer: _frameBuffers[swImgIndex],
                                             clearValueCount: (uint)clearValues.Length,
                                             pClearValues: clearValues.AsPtr());

        vk.CmdBeginRenderPass(_mainCommandBuffer, rpInfo, SubpassContents.Inline);

        // RENDERING COMMANDS
        vk.CmdBindPipeline(_mainCommandBuffer, PipelineBindPoint.Graphics, _meshPipeline);

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
                vk.CmdBindVertexBuffers(_mainCommandBuffer, 0, 1, render.RenderBuffer!.Buffer, 0);

                var e = chunk.Entities[index];

                if (!e.TryGet<Scale>(out var scale)) scale = new();
                if (!e.TryGet<Rotation>(out var rot)) rot = new();
                if (!e.TryGet<Translation>(out var translation)) translation = new();

                //model rotation
                var model = Transform.ToMatrix(scale, rot, translation);


                var meshMatrix = model * view * projection;
                var constants = (Vector4.Zero, meshMatrix);


                vk.CmdPushConstants(_mainCommandBuffer, _meshPipeline.Layout, ShaderStageFlags.VertexBit, 0, size,
                                    ref constants);

                vk.CmdDraw(_mainCommandBuffer, (uint)render.Mesh.Vertices.Count, 1, 0, 0);
            }
        }

        vk.CmdEndRenderPass(_mainCommandBuffer);
        vk.EndCommandBuffer(_mainCommandBuffer);

        // prepare the submission to the queue.
        // we want to wait on the _presentSemaphore, as that semaphore is signaled when the swapchain is ready
        // we will signal the _renderSemaphore, to signal that rendering has finished
        var waitStage = PipelineStageFlags.ColorAttachmentOutputBit;

        fixed (CommandBuffer* cmd = &_mainCommandBuffer)
        fixed (Semaphore* present = &_presentSemaphore, render = &_renderSemaphore)
        {
            var submit = new SubmitInfo(pWaitDstStageMask: &waitStage,
                                        waitSemaphoreCount: 1, pWaitSemaphores: present,
                                        signalSemaphoreCount: 1, pSignalSemaphores: render,
                                        commandBufferCount: 1, pCommandBuffers: cmd);

            // submit command buffer to the queue and execute it.
            // _renderFence will now block until the graphic commands finish execution
            vk.QueueSubmit(_graphicsQueue, 1, submit, _renderFence);
        }

        // this will put the image we just rendered into the visible window.
        // we want to wait on the _renderSemaphore for that,
        // as it's necessary that drawing commands have finished before the image is displayed to the user
        fixed (SwapchainKHR* swapchain = &_swapchain.Swapchain)
        fixed (Semaphore* semaphore = &_renderSemaphore)
        {
            var presentInfo = new PresentInfoKHR(swapchainCount: 1, pSwapchains: swapchain,
                                                 waitSemaphoreCount: 1, pWaitSemaphores: semaphore,
                                                 pImageIndices: &swImgIndex);

            res = _swapchain.QueuePresent(_graphicsQueue, presentInfo);
            if (res is Result.ErrorOutOfDateKhr or Result.SuboptimalKhr)
            {
                return;
            }

            if (res is not Result.Success)
            {
                res.LogError("Failed to present swapchain image");
                return;
            }
        }

        _frameNumber++;
    }

    public unsafe void UploadMesh(ref RenderMesh renderMesh)
    {
        ref var mesh = ref renderMesh.Mesh;

        var bufferInfo = new BufferCreateInfo(size: mesh.Vertices.SizeOf(), usage: BufferUsageFlags.VertexBufferBit);
        var allocInfo = new AllocationCreateInfo(usage: MemoryUsage.CPU_To_GPU);

        renderMesh.RenderBuffer = new VulkanBuffer(_allocator, bufferInfo, allocInfo)
            .DisposeWith(_bufferDisposable);

        using var mapping = renderMesh.RenderBuffer.Map<Vertex>();
        if (!mapping.TryGetSpan(out var span))
            throw new AllocationException("Couldn't get vertices span from allocation");

        mesh.Vertices.AsSpan().CopyTo(span);
    }

    private void InitVulkan()
    {
        _instance = new InstanceBuilder
                    {
                        ApplicationName = "App",
                        EngineName = "GamesDotNet",
                        EngineVersion = new Version32(0, 0, 1),
                        RequiredApiVersion = Vk.Version11,
                        Extensions = GetGlfwRequiredVulkanExtensions(),
                        IsHeadless = false,
#if DEBUG
                        EnabledValidationFeatures = new List<ValidationFeatureEnableEXT>
                        {
                            ValidationFeatureEnableEXT.BestPracticesExt,
                            ValidationFeatureEnableEXT.SynchronizationValidationExt,
                            ValidationFeatureEnableEXT.DebugPrintfExt,
                            ValidationFeatureEnableEXT.GpuAssistedReserveBindingSlotExt
                        },
                        IsValidationLayersRequested = true
#endif
                    }
#if DEBUG
                    .UseDefaultDebugMessenger()
#endif
                    .Build();

        _surface = CreateSurface(_window);

        var selected = new PhysicalDeviceSelector(_instance, _surface, new()
        {
            RequiredVersion = Vk.Version11
        }).Select();
        _physDevice = selected.Device;

        _device = new DeviceBuilder(_instance, selected).Build();

        _allocator = new(new(_instance.VkVersion, _instance.Vk, _instance, _physDevice, _device));

        CreateSwapchain();
        _graphicsQueue = _device.GetQueue(QueueType.Graphics)!.Value;
    }

    private unsafe VulkanSurface CreateSurface(IVkSurfaceSource window)
    {
        Debug.Assert(window.VkSurface != null, "window.VkSurface != null");

        var handle = window.VkSurface.Create<nint>(_instance.Instance.ToHandle(), null);
        return new(_instance, handle.ToSurface());
    }

    private bool CreateSwapchain()
    {
        var swapchain = new SwapchainBuilder(_instance, _physDevice, _device, new SwapchainBuilder.Info(_surface)
            {
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

        _depthImage = new(_instance.Vk, _device, _allocator, depthImgInfo, allocInfo);
        _depthImageView = _depthImage.GetImageView(DepthFormat, ImageAspectFlags.DepthBit);

        return true;
    }

    private unsafe void CreateCommands()
    {
        var commandPoolInfo =
            new CommandPoolCreateInfo(flags: CommandPoolCreateFlags.ResetCommandBufferBit,
                                      queueFamilyIndex: _device.GetQueueFamilyIndex(QueueType.Graphics)!.Value);


        _instance.Vk.CreateCommandPool(_device, commandPoolInfo, NullAlloc, out _commandPool);

        var bufferAllocInfo =
            new CommandBufferAllocateInfo(commandPool: _commandPool,
                                          commandBufferCount: 1, level: CommandBufferLevel.Primary);

        _instance.Vk.AllocateCommandBuffers(_device, bufferAllocInfo, out _mainCommandBuffer);
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

        _instance.Vk.CreateRenderPass(_device, renderPassInfo, NullAlloc, out _renderPass);
    }

    private unsafe void CreateFrameBuffers()
    {
        var frameBuffers = new Framebuffer[_swapchain!.ImageCount];
        var fbInfo = new FramebufferCreateInfo(renderPass: _renderPass,
                                               width: _swapchain.Extent.Width, height: _swapchain.Extent.Height,
                                               attachmentCount: 2, layers: 1);

        foreach (var (imageView, i) in _swapchain.GetImageViews().WithIndex())
        {
            var attachments = new[] { imageView, _depthImageView!.ImageView };
            fbInfo.PAttachments = attachments.AsPtr();
            _instance.Vk.CreateFramebuffer(_device, fbInfo, NullAlloc, out frameBuffers[i]);
        }

        _frameBuffers = frameBuffers;
    }

    private unsafe void CreateSyncStructures()
    {
        var fenceCreateInfo = new FenceCreateInfo(flags: FenceCreateFlags.SignaledBit);

        _instance.Vk.CreateFence(_device, fenceCreateInfo, NullAlloc, out _renderFence);

        var semaphoreInfo = new SemaphoreCreateInfo();

        _instance.Vk.CreateSemaphore(_device, semaphoreInfo, NullAlloc, out _presentSemaphore);
        _instance.Vk.CreateSemaphore(_device, semaphoreInfo, NullAlloc, out _renderSemaphore);
    }

    private void CreatePipeline()
    {
        _meshPipeline = new PipelineBuilder(_instance, _device)
            .Build(new()
            {
                VertexInputDescription = _meshVertShader.GetVertexDescription(),
                ShaderStages = new[] { _meshFragShader, _meshVertShader },
                RenderPass = _renderPass,
                Viewport = new(0, 0, _window.Size.X, _window.Size.Y, 0, 1f),
                Scissor = new(new(0, 0), _swapchain!.Extent),
                EnableDepthTest = true, EnableDepthWrite = true, DepthStencilCompare = CompareOp.LessOrEqual
            });
    }

    private void RecreateSwapChain()
    {
        var vk = _instance.Vk;
        vk.DeviceWaitIdle(_device);

        foreach (var framebuffer in _frameBuffers) vk.DestroyFramebuffer(_device, framebuffer, NullAlloc);
        vk.DestroyCommandPool(_device, _commandPool, NullAlloc);
        _meshPipeline.Dispose();

        if (!CreateSwapchain()) return;
        CreateFrameBuffers();
        CreatePipeline();
        CreateCommands();
    }

    private unsafe IEnumerable<string> GetGlfwRequiredVulkanExtensions()
    {
        Debug.Assert(_window.VkSurface != null, "_window.VkSurface != null");
        var ppExtensions = _window.VkSurface.GetRequiredExtensions(out var count);

        if (ppExtensions is null)
            throw new PlatformException("Vulkan extensions for windowing not available");
        return SilkMarshal.PtrToStringArray((nint)ppExtensions, (int)count);
    }
}