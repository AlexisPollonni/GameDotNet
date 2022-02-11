using Core.Graphics.Vulkan.Bootstrap;
using Serilog;
using Serilog.Events;
using Silk.NET.Core;
using Silk.NET.GLFW;
using Silk.NET.Input;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using MemoryExtensions = Core.Tools.Extensions.MemoryExtensions;

namespace Core;

public class Application
{
    private readonly IView _mainView;

    public Application(string appName)
    {
        if (string.IsNullOrWhiteSpace(appName))
            throw new ArgumentException("Application name can't be null or empty", nameof(appName));
        ApplicationName = appName;

        CreateLogger();

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
            opt.Size = new(800, 600);
            opt.Title = "Test";

            _mainView = Window.Create(opt);
        }

        _mainView.Load += OnWindowLoad;
    }

    public string ApplicationName { get; }

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

            var surfaceHandle = _mainView.VkSurface.Create<IntPtr>(instance.Instance.ToHandle(), null);
            var surface = surfaceHandle.ToSurface();

            var physDevice = new PhysicalDeviceSelector(instance, surface).Select();

            var device = new DeviceBuilder(instance.Vk, physDevice).Build();
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

    private void CreateLogger()
    {
        Log.Logger = new LoggerConfiguration()
                     .MinimumLevel.Verbose()
                     .WriteTo.Console(LogEventLevel.Information)
                     .WriteTo.Debug(LogEventLevel.Debug)
                     .WriteTo.File(Path.Combine(Constants.LogsDirectoryPath, ApplicationName, "game.log"),
                                   rollingInterval: RollingInterval.Minute, retainedFileCountLimit: 10)
                     .CreateLogger();

        Log.Information("Log started for {ApplicationName}", ApplicationName);
        Log.Information(@"
Is 64 bit: {Is64Bit}
Running directory: {RunningDirectory}
.NET version: {NetVersion}",
                        Environment.Is64BitProcess,
                        Environment.CurrentDirectory,
                        Environment.Version);
    }
}