using Core.Graphics.Vulkan;
using Serilog;
using Serilog.Events;
using Silk.NET.Core;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using ApplicationInfo = Core.Graphics.Vulkan.ApplicationInfo;

namespace Core;

public class Application
{
    private readonly VulkanContext _context;
    private readonly IView _mainView;

    public Application(string appName)
    {
        if (string.IsNullOrWhiteSpace(appName))
            throw new ArgumentException("Application name can't be null or empty", nameof(appName));
        ApplicationName = appName;

        CreateLogger();

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
            var instance = _context.CreateInstance(new ApplicationInfo("App", Vk.Version12, new Version32(0, 0, 1)));

            var surfaceHandle = _mainView.VkSurface.Create<IntPtr>(instance.Instance.ToHandle(), null);
            var surface = surfaceHandle.ToSurface();

            var device = instance.PickPhysicalDeviceForSurface(surface);
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