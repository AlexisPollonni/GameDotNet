using GameDotNet.Core;
using GameDotNet.Graphics.Vulkan;
using GameDotNet.Management;
using GameDotNet.Management.ECS;
using Serilog;
using Serilog.Events;
using Silk.NET.Input;
using Silk.NET.Windowing;

namespace GameDotNet.Hosting;

public class Application : IDisposable
{
    public Universe Universe { get; }

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

        Universe = new();
        _mainView.Load += OnWindowLoad;
        _mainView.Update += d => Universe.Update();

        Universe.RegisterSystem(new VulkanRenderSystem(_mainView));
        Universe.RegisterSystem(new CameraSystem(_mainView));
    }

    public string ApplicationName { get; }

    public int Run()
    {
        _mainView.Run();

        return 0;
    }

    public void Dispose()
    {
        Universe.Dispose();
        _mainView.Dispose();

        GC.SuppressFinalize(this);
    }

    private void OnWindowLoad()
    {
        using var input = _mainView.CreateInput();
        foreach (var kb in input.Keyboards)
        {
            kb.KeyUp += (_, key, _) =>
            {
                if (key == Key.Escape)
                {
                    _mainView.Close();
                }
            };
        }

        Universe.Initialize();
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