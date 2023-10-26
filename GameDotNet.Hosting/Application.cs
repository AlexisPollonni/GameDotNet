using System.IO.Compression;
using GameDotNet.Core;
using GameDotNet.Graphics.Vulkan;
using GameDotNet.Graphics.WGPU;
using GameDotNet.Management;
using GameDotNet.Management.ECS;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;
using Serilog.Formatting.Compact;
using Serilog.Sinks.File.GZip;
using Silk.NET.Input;
using Silk.NET.Windowing;

namespace GameDotNet.Hosting;

public class Application : IDisposable
{
    public string ApplicationName { get; }
    public Universe Universe { get; }

    private readonly IView _mainView;
    private readonly TaskCompletionSource _loadTcs;


    public Application(string appName)
    {
        if (string.IsNullOrWhiteSpace(appName))
            throw new ArgumentException("Application name can't be null or empty", nameof(appName));
        ApplicationName = appName;

        CreateLogger();

        Window.PrioritizeGlfw();
        
        var api = new GraphicsAPI(ContextAPI.None, new(1, 0));
        
        if (Window.IsViewOnly)
        {
            var opt = ViewOptions.Default;
            opt.API = api;
            _mainView = Window.GetView(opt);
        }
        else
        {
            var opt = WindowOptions.Default;
            opt.API = api;
            opt.VSync = true;
            opt.Size = new(800, 600);
            opt.Title = "Test";

            _mainView = Window.Create(opt);
        }

        Universe = new();
        _loadTcs = new();
        
        _mainView.Load += () => _loadTcs.SetResult();
        _mainView.Update += d => Universe.Update();
        _mainView.Closing += () => _loadTcs.TrySetCanceled();

        var log = new SerilogLoggerFactory().CreateLogger<VulkanRenderSystem>();
        
        Universe.RegisterSystem(new WebGpuRenderer(log, _mainView));
        Universe.RegisterSystem(new CameraSystem(_mainView));
    }

    public async Task<int> Run()
    {
        _mainView.Initialize();
        await _loadTcs.Task;
        
        //https://stackoverflow.com/questions/39271492/how-do-i-create-a-custom-synchronizationcontext-so-that-all-continuations-can-be
        AsyncContext.Run(OnWindowLoad);

        _mainView.Run();
        
        return 0;
    }

    public void Dispose()
    {
        Universe.Dispose();
        _mainView.Dispose();

        Log.CloseAndFlush();

        GC.SuppressFinalize(this);
    }

    private async Task OnWindowLoad()
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

        await Universe.Initialize();
    }

    private void CreateLogger()
    {
        var logDirPath = Path.Combine(Constants.LogsDirectoryPath, ApplicationName);
        var logPath = Path.Combine(logDirPath, "game.gz");

        var monitor = new AsyncSinkMonitorHook();

        foreach (var file in Directory.EnumerateFiles(logDirPath, "*.gz"))
            File.Delete(file);

        Log.Logger = new LoggerConfiguration()
                     .Enrich.FromLogContext()
                     .MinimumLevel.Verbose()
                     .WriteTo.Async(a =>
                     {
                         a.Console(LogEventLevel.Debug);
                         a.File(new CompactJsonFormatter(), logPath,
                                hooks: new GZipHooks(CompressionLevel.SmallestSize),
                                retainedFileCountLimit: 5, rollOnFileSizeLimit: true, buffered: true);
                     }, monitor, 100000)
                     .CreateLogger();

        monitor.SelfLog = Log.Logger;

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