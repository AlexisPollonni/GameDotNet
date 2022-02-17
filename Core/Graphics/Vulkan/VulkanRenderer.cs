using System.Diagnostics;
using Core.Graphics.Vulkan.Bootstrap;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.GLFW;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;

namespace Core.Graphics.Vulkan;

public sealed class VulkanRenderer : IDisposable
{
    private readonly IView _window;
    private VulkanDevice _device = null!;
    private VulkanInstance _instance = null!;
    private VulkanPhysDevice _physDevice = null!;
    private VulkanSurface _surface = null!;
    private VulkanSwapchain _swapchain = null!;


    public VulkanRenderer(IView window)
    {
        _window = window;

        _window.Load += Initialize;
    }

    public void Dispose()
    {
        _swapchain.Dispose();
        _device.Dispose();
        _instance.Dispose();
        _surface.Dispose();
    }

    private void Initialize()
    {
        if (_window.VkSurface is null)
        {
            throw new NullReferenceException();
        }

        _instance = new InstanceBuilder
            {
                ApplicationName = "App",
                EngineName = "GamesDotNet",
                EngineVersion = new Version32(0, 0, 1),
                DesiredApiVersion = Vk.Version11,
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

        _physDevice = new PhysicalDeviceSelector(_instance, _surface).Select();

        _device = new DeviceBuilder(_instance, _physDevice).Build();

        _swapchain = new SwapchainBuilder(_instance, _physDevice, _device, _surface)
            .Build();
    }

    private unsafe VulkanSurface CreateSurface(IView window)
    {
        Debug.Assert(window.VkSurface != null, "window.VkSurface != null");

        var handle = window.VkSurface.Create<nint>(_instance.Instance.ToHandle(), null);
        return new(_instance, handle.ToSurface());
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