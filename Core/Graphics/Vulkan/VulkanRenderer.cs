using System.Diagnostics;
using System.Runtime.CompilerServices;
using Core.Graphics.Vulkan.Bootstrap;
using Core.Tools.Extensions;
using Silk.NET.Core;
using Silk.NET.Core.Contexts;
using Silk.NET.Core.Native;
using Silk.NET.GLFW;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace Core.Graphics.Vulkan;

public sealed class VulkanRenderer : IDisposable
{
    private readonly IView _window;
    private CommandPool _commandPool;
    private VulkanDevice _device = null!;
    private Framebuffer[] _framebuffers;
    private ulong _frameNumber;

    private Queue _graphicsQueue;
    private VulkanInstance _instance = null!;
    private CommandBuffer _mainCommandBuffer;
    private VulkanPhysDevice _physDevice = null!;
    private Semaphore _presentSemaphore, _renderSemaphore;
    private Fence _renderFence;
    private RenderPass _renderPass;
    private VulkanSurface _surface = null!;
    private VulkanSwapchain _swapchain = null!;

    public VulkanRenderer(IView window)
    {
        _window = window;
        _framebuffers = Array.Empty<Framebuffer>();

        _window.Load += Initialize;
        _window.Render += Draw;
    }

    private static ref readonly AllocationCallbacks NullAlloc => ref Unsafe.NullRef<AllocationCallbacks>();

    public void Dispose()
    {
        _instance.Vk.DestroyRenderPass(_device, _renderPass, NullAlloc);
        foreach (var framebuffer in _framebuffers)
        {
            _instance.Vk.DestroyFramebuffer(_device, framebuffer, NullAlloc);
        }

        _instance.Vk.DestroyCommandPool(_device, _commandPool, NullAlloc);

        _swapchain.Dispose();
        _device.Dispose();
        _surface.Dispose();
        _instance.Dispose();
    }

    private void Initialize()
    {
        InitVulkan();
        CreateCommands();
        CreateRenderPass();
        CreateFramebuffers();
        CreateSyncStructures();
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
                EnabledValidationFeatures = new List<ValidationFeatureEnableEXT>
                {
                    ValidationFeatureEnableEXT.ValidationFeatureEnableBestPracticesExt,
                    ValidationFeatureEnableEXT.ValidationFeatureEnableSynchronizationValidationExt,
                    ValidationFeatureEnableEXT.ValidationFeatureEnableGpuAssistedExt,
                    ValidationFeatureEnableEXT.ValidationFeatureEnableDebugPrintfExt
                },
                IsValidationLayersRequested = true,
                IsHeadless = false
            }.UseDefaultDebugMessenger()
             .Build();

        _surface = CreateSurface(_window);

        _physDevice = new PhysicalDeviceSelector(_instance, _surface, new()
        {
            RequiredVersion = Vk.Version11
        }).Select();

        _device = new DeviceBuilder(_instance, _physDevice).Build();

        _swapchain = new SwapchainBuilder(_instance, _physDevice, _device, new SwapchainBuilder.Info(_surface)
            {
                DesiredPresentModes = new() { PresentModeKHR.PresentModeFifoKhr }
            })
            .Build();

        _graphicsQueue = _device.GetQueue(QueueType.Graphics)!.Value;
    }

    private unsafe VulkanSurface CreateSurface(IVkSurfaceSource window)
    {
        Debug.Assert(window.VkSurface != null, "window.VkSurface != null");

        var handle = window.VkSurface.Create<nint>(_instance.Instance.ToHandle(), null);
        return new(_instance, handle.ToSurface());
    }

    private unsafe void CreateCommands()
    {
        var commandPoolInfo =
            new CommandPoolCreateInfo(flags: CommandPoolCreateFlags.CommandPoolCreateResetCommandBufferBit,
                                      queueFamilyIndex: _device.GetQueueIndex(QueueType.Graphics)!.Value);


        _instance.Vk.CreateCommandPool(_device, commandPoolInfo, NullAlloc, out _commandPool);

        var bufferAllocInfo =
            new CommandBufferAllocateInfo(commandPool: _commandPool,
                                          commandBufferCount: 1, level: CommandBufferLevel.Primary);

        _instance.Vk.AllocateCommandBuffers(_device, bufferAllocInfo, out _mainCommandBuffer);
    }

    private unsafe void CreateRenderPass()
    {
        var colorAttachment = new AttachmentDescription(format: _swapchain.ImageFormat,
                                                        samples: SampleCountFlags.SampleCount1Bit,
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

    private unsafe void CreateFramebuffers()
    {
        var framebuffers = new Framebuffer[_swapchain.ImageCount];
        var fbInfo = new FramebufferCreateInfo(renderPass: _renderPass,
                                               width: _swapchain.Extent.Width, height: _swapchain.Extent.Height,
                                               attachmentCount: 1, layers: 1);

        foreach (var (imageView, i) in _swapchain.GetImageViews().WithIndex())
        {
            fbInfo.PAttachments = &imageView;
            _instance.Vk.CreateFramebuffer(_device, fbInfo, NullAlloc, out framebuffers[i]);
        }

        _framebuffers = framebuffers;
    }

    private unsafe void CreateSyncStructures()
    {
        var fenceCreateInfo = new FenceCreateInfo(flags: FenceCreateFlags.FenceCreateSignaledBit);

        _instance.Vk.CreateFence(_device, fenceCreateInfo, NullAlloc, out _renderFence);

        var semaphoreInfo = new SemaphoreCreateInfo();

        _instance.Vk.CreateSemaphore(_device, semaphoreInfo, NullAlloc, out _presentSemaphore);
        _instance.Vk.CreateSemaphore(_device, semaphoreInfo, NullAlloc, out _renderSemaphore);
    }

    private unsafe void Draw(double d)
    {
        var vk = _instance.Vk;
        //wait until the GPU has finished rendering the last frame. Timeout of 1 second
        vk.WaitForFences(_device, 1, _renderFence, true, 1000000000);
        vk.ResetFences(_device, 1, _renderFence);

        var swImgIndex = _swapchain.AcquireNextImage(1000000000, _presentSemaphore, null);

        vk.ResetCommandBuffer(_mainCommandBuffer, 0);

        var cmdBeginInfo =
            new CommandBufferBeginInfo(flags: CommandBufferUsageFlags.CommandBufferUsageOneTimeSubmitBit);

        vk.BeginCommandBuffer(_mainCommandBuffer, cmdBeginInfo);

        //make a clear-color from frame number. This will flash with a 120*pi frame period.
        var clearValue = new ClearValue(new(0, 0, (float)Math.Abs(Math.Sin(_frameNumber / 120D)), 0));

        var rpInfo = new RenderPassBeginInfo(renderPass: _renderPass,
                                             renderArea: new Rect2D(new Offset2D(0, 0), _swapchain.Extent),
                                             framebuffer: _framebuffers[swImgIndex],
                                             clearValueCount: 1,
                                             pClearValues: &clearValue);

        vk.CmdBeginRenderPass(_mainCommandBuffer, rpInfo, SubpassContents.Inline);

        vk.CmdEndRenderPass(_mainCommandBuffer);
        vk.EndCommandBuffer(_mainCommandBuffer);

        //prepare the submission to the queue.
        //we want to wait on the _presentSemaphore, as that semaphore is signaled when the swapchain is ready
        //we will signal the _renderSemaphore, to signal that rendering has finished
        var waitStage = PipelineStageFlags.PipelineStageColorAttachmentOutputBit;

        fixed (CommandBuffer* cmd = &_mainCommandBuffer)
        fixed (Semaphore* present = &_renderSemaphore, render = &_renderSemaphore)
        {
            var submit = new SubmitInfo(pWaitDstStageMask: &waitStage,
                                        waitSemaphoreCount: 1, pWaitSemaphores: present,
                                        signalSemaphoreCount: 1, pSignalSemaphores: render,
                                        commandBufferCount: 1, pCommandBuffers: cmd);

            //submit command buffer to the queue and execute it.
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

            _swapchain.QueuePresent(_graphicsQueue, presentInfo);
        }

        _frameNumber++;
    }

    private static IEnumerable<string> GetGlfwRequiredVulkanExtensions()
    {
        unsafe
        {
            var ppExtensions = Glfw.GetApi().GetRequiredInstanceExtensions(out var count);

            if (ppExtensions is null)
                throw new PlatformException("GLFW vulkan extensions for windowing not available");
            return SilkMarshal.PtrToStringArray((nint)ppExtensions, (int)count);
        }
    }
}