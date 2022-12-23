using System.Diagnostics;
using Arch.Core;
using GameDotNet.Core.Graphics.MemoryAllocation;
using GameDotNet.Core.Graphics.Vulkan.Bootstrap;
using GameDotNet.Core.Shaders.Generated;
using GameDotNet.Core.Tools.Containers;
using GameDotNet.Core.Tools.Extensions;
using Microsoft.Toolkit.HighPerformance;
using Silk.NET.Core;
using Silk.NET.Core.Contexts;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using static GameDotNet.Core.Graphics.Vulkan.Constants;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace GameDotNet.Core.Graphics.Vulkan;

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

    private Queue _graphicsQueue;
    private Framebuffer[] _frameBuffers;
    private CommandPool _commandPool;
    private CommandBuffer _mainCommandBuffer;
    private RenderPass _renderPass;
    private Semaphore _presentSemaphore, _renderSemaphore;
    private Fence _renderFence;

    public VulkanRenderer(IView window)
    {
        _window = window;
        _frameBuffers = Array.Empty<Framebuffer>();
        _bufferDisposable = new();
    }

    public void Dispose()
    {
        _bufferDisposable.Dispose();
        _instance.Vk.DestroyRenderPass(_device, _renderPass, NullAlloc);
        foreach (var framebuffer in _frameBuffers)
        {
            _instance.Vk.DestroyFramebuffer(_device, framebuffer, NullAlloc);
        }

        _instance.Vk.DestroyCommandPool(_device, _commandPool, NullAlloc);

        // Order is important
        _allocator.Dispose();
        _swapchain?.Dispose();
        _device.Dispose();
        _surface.Dispose();
        _instance.Dispose();
    }

    public void Initialize()
    {
        InitVulkan();
        CreateCommands();
        CreateRenderPass();
        CreateFrameBuffers();
        CreateSyncStructures();
        CreatePipeline();
    }

    public unsafe void Draw(TimeSpan dt, QueryChunkIterator chunks)
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

        var rpInfo = new RenderPassBeginInfo(renderPass: _renderPass,
                                             renderArea: new Rect2D(new Offset2D(0, 0), _swapchain.Extent),
                                             framebuffer: _frameBuffers[swImgIndex],
                                             clearValueCount: 1,
                                             pClearValues: &clearValue);

        vk.CmdBeginRenderPass(_mainCommandBuffer, rpInfo, SubpassContents.Inline);

        // RENDERING COMMANDS
        vk.CmdBindPipeline(_mainCommandBuffer, PipelineBindPoint.Graphics, _meshPipeline);

        foreach (ref var chunk in chunks)
        {
            foreach (ref var render in chunk.GetSpan<RenderMesh>())
            {
                vk.CmdBindVertexBuffers(_mainCommandBuffer, 0, 1, render.Mesh.Buffer, 0);
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

    public unsafe void UploadMesh(ref Mesh mesh)
    {
        var bufferInfo =
            new
                BufferCreateInfo(size: (ulong)(sizeof(Vertex) * mesh.Vertices.Count),
                                 usage: BufferUsageFlags.VertexBufferBit);

        var allocInfo = new AllocationCreateInfo(usage: MemoryUsage.CPU_To_GPU);

        mesh.Buffer = _allocator.CreateBuffer(bufferInfo, allocInfo, out var allocation);

        allocation.DisposeWith(_bufferDisposable);

        allocation.Map();
        if (!allocation.TryGetSpan(out Span<Vertex> span))
            throw new AllocationException("Couldn't get vertices span from allocation");

        mesh.Vertices.AsSpan().CopyTo(span);
        allocation.Unmap();
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
                            ValidationFeatureEnableEXT.ValidationFeatureEnableBestPracticesExt,
                            ValidationFeatureEnableEXT.ValidationFeatureEnableSynchronizationValidationExt,
                            ValidationFeatureEnableEXT.ValidationFeatureEnableDebugPrintfExt,
                            ValidationFeatureEnableEXT.ValidationFeatureEnableGpuAssistedReserveBindingSlotExt
                        },
                        IsValidationLayersRequested = true
#endif
            }
#if DEBUG
                    .UseDefaultDebugMessenger()
#endif
            .Build();

        _surface = CreateSurface(_window);

        _physDevice = new PhysicalDeviceSelector(_instance, _surface, new()
        {
            RequiredVersion = Vk.Version11
        }).Select();

        _device = new DeviceBuilder(_instance, _physDevice).Build();

        CreateSwapchain();
        _graphicsQueue = _device.GetQueue(QueueType.Graphics)!.Value;

        _allocator = new(new(_instance.VkVersion, _instance.Vk, _instance, _physDevice, _device));
    }

    private unsafe VulkanSurface CreateSurface(IVkSurfaceSource window)
    {
        Debug.Assert(window.VkSurface != null, "window.VkSurface != null");

        var handle = window.VkSurface.Create<nint>(_instance.Instance.ToHandle(), null);
        return new(_instance, handle.ToSurface());
    }

    private void CreateSwapchain()
    {
        var swapchain = new SwapchainBuilder(_instance, _physDevice, _device, new SwapchainBuilder.Info(_surface)
            {
                DesiredPresentModes = new() { PresentModeKHR.FifoKhr },
                OldSwapchain = _swapchain
            })
            .Build();

        _swapchain?.Dispose();
        _swapchain = swapchain;
    }

    private unsafe void CreateCommands()
    {
        var commandPoolInfo =
            new CommandPoolCreateInfo(flags: CommandPoolCreateFlags.ResetCommandBufferBit,
                                      queueFamilyIndex: _device.GetQueueIndex(QueueType.Graphics)!.Value);


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

        var subpass = new SubpassDescription(pipelineBindPoint: PipelineBindPoint.Graphics,
                                             colorAttachmentCount: 1, pColorAttachments: &colorAttachmentRef);

        var renderPassInfo = new RenderPassCreateInfo(attachmentCount: 1, pAttachments: &colorAttachment,
                                                      subpassCount: 1, pSubpasses: &subpass);

        _instance.Vk.CreateRenderPass(_device, renderPassInfo, NullAlloc, out _renderPass);
    }

    private unsafe void CreateFrameBuffers()
    {
        var frameBuffers = new Framebuffer[_swapchain!.ImageCount];
        var fbInfo = new FramebufferCreateInfo(renderPass: _renderPass,
                                               width: _swapchain.Extent.Width, height: _swapchain.Extent.Height,
                                               attachmentCount: 1, layers: 1);

        foreach (var (imageView, i) in _swapchain.GetImageViews().WithIndex())
        {
            fbInfo.PAttachments = &imageView;
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
        using var meshFragShader = new VulkanShader(_instance.Vk, _device, ShaderStageFlags.FragmentBit,
                                                    CompiledShaders.MeshFragmentShader);
        using var meshVertShader = new VulkanShader(_instance.Vk, _device, ShaderStageFlags.VertexBit,
                                                    CompiledShaders.MeshVertexShader);

        _meshPipeline = new PipelineBuilder(_instance, _device)
            .Build(new()
            {
                VertexInputDescription = Vertex.GetDescription(),
                ShaderStages = new[] { meshFragShader, meshVertShader },
                RenderPass = _renderPass,
                Viewport = new(0, 0,
                               _window.Size.X, _window.Size.Y, 0, 1f),
                Scissor =
                    new(new(0, 0),
                        new Extent2D((uint)_window.Size.X,
                                     (uint)_window.Size.Y))
            });
    }

    private void RecreateSwapChain()
    {
        var vk = _instance.Vk;
        vk.DeviceWaitIdle(_device);

        vk.DestroyCommandPool(_device, _commandPool, NullAlloc);
        foreach (var framebuffer in _frameBuffers)
        {
            vk.DestroyFramebuffer(_device, framebuffer, NullAlloc);
        }

        CreateSwapchain();
        CreateFrameBuffers();
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