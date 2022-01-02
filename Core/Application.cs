using Core.Graphics.Vulkan;
using Core.Graphics.Vulkan.Bootstrap;
using Silk.NET.Core;
using Silk.NET.GLFW;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using MemoryExtensions = Core.Tools.Extensions.MemoryExtensions;

namespace Core;

public class Application
{
    private readonly VulkanContext _context;
    private readonly IView _mainView;

    public Application()
    {
        _context = new VulkanContext();

        Window.PrioritizeGlfw();

        if (Window.IsViewOnly)
        {
            var opt = ViewOptions.DefaultVulkan;

            _mainView = Window.GetView(opt);
        }
        else
        {
            var opt = WindowOptions.DefaultVulkan;
            opt.VSync = true;
            opt.Size = new Vector2D<int>(800, 600);
            opt.Title = "Test";

            _mainView = Window.Create(opt);
        }

        _mainView.Load += OnWindowLoad;
    }

    public int Run()
    {
        _mainView.Run();

        return 0;
    }

    private unsafe void OnWindowLoad()
    {
        var input = _mainView.CreateInput();
        foreach (var kb in input.Keyboards)
        {
            kb.KeyUp += (keyboard, key, arg3) =>
            {
                if (key == Key.Escape)
                {
                    _mainView.Close();
                }
            };
        }

        if (_mainView.VkSurface is not null)
        {
            var instance = new InstanceBuilder
                {
                    ApplicationName = "App",
                    EngineName = "GamesDotNet",
                    EngineVersion = new Version32(0, 0, 1),
                    DesiredApiVersion = Vk.Version11,
                    Extensions = GetGlfwRequiredVulkanExtensions(),
                    IsValidationLayersRequested = true,
                    IsHeadless = false
                }.UseDefaultDebugMessenger()
                 .Build();

            var surfaceHandle = _mainView.VkSurface.Create<IntPtr>(instance.Instance.ToHandle(), null);
            var surface = surfaceHandle.ToSurface();

            var device = instance.PickPhysicalDeviceForSurface(surface);
        }
    }

    private IEnumerable<string> GetGlfwRequiredVulkanExtensions()
    {
        unsafe
        {
            var ppExts = Glfw.GetApi().GetRequiredInstanceExtensions(out var count);

            if (ppExts is null)
                throw new PlatformException("GLFW vulkan extensions for windowing not available");

            return MemoryExtensions.FromPtrStrArray(ppExts, count);
        }
    }
}