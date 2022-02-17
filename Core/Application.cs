using Core.Graphics.Vulkan;
using Serilog;
using Serilog.Events;
using Silk.NET.Input;
using Silk.NET.Windowing;

namespace Core;

public class Application : IDisposable
{
    private readonly IView _mainView;

    private readonly VulkanRenderer _renderer;

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
        _renderer = new(_mainView);
    }

    public string ApplicationName { get; }

    public void Dispose()
    {
        _renderer.Dispose();
        _mainView.Dispose();

        GC.SuppressFinalize(this);
    }

    public int Run()
    {
        _mainView.Run();

        return 0;
    }

    private void OnWindowLoad()
    {
        var input = _mainView.CreateInput();
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