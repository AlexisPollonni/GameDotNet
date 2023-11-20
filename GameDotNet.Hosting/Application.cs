using System.IO.Compression;
using GameDotNet.Core;
using GameDotNet.Graphics;
using GameDotNet.Graphics.WGPU;
using GameDotNet.Management;
using GameDotNet.Management.ECS;
using MessagePipe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Sinks.File.GZip;
using Silk.NET.Input;
using Silk.NET.Windowing;
using ILogger = Serilog.ILogger;

namespace GameDotNet.Hosting;

public class Application : IDisposable
{
    public string ApplicationName { get; }
    public IHost GlobalHost { get; private set; }

    private readonly IView _mainView;
    private readonly Task _loaded;


    public Application(string appName)
    {
        if (string.IsNullOrWhiteSpace(appName))
            throw new ArgumentException("Application name can't be null or empty", nameof(appName));
        ApplicationName = appName;

        Log.Logger = CreateLogger(appName);

        _mainView = CreateSilkView();

        var tcs = new TaskCompletionSource();
        
        _mainView.Load += () => tcs.SetResult();
        _mainView.Closing += () => tcs.TrySetCanceled();
        _loaded = tcs.Task;
        
        var hostBuilder = CreateHostBuilder(Log.Logger);
        
        hostBuilder.Services.AddCoreSystemServices();
        
        GlobalHost = hostBuilder.Build();
    }

    public async Task Initialize()
    {
        _mainView.Initialize();
        await _loaded;
        
        //https://stackoverflow.com/questions/39271492/how-do-i-create-a-custom-synchronizationcontext-so-that-all-continuations-can-be
        AsyncContext.Run(InitializeCore);
    }

    public async Task<int> Run()
    {
        if (!_mainView.IsInitialized)
            throw new InvalidOperationException("Engine not initialized");
        
        _mainView.Run();

        await GlobalHost.StopAsync();
        
        return 0;
    }

    public void Dispose()
    {
        GlobalHost.Dispose();
        _mainView.Dispose();

        Log.CloseAndFlush();

        GC.SuppressFinalize(this);
    }

    public static ILogger CreateLogger(string appName, LogEventLevel minConsoleLevel = LogEventLevel.Debug, LogEventLevel minFileLevel = LogEventLevel.Verbose)
    {
        var logDirPath = Path.Combine(Constants.LogsDirectoryPath, appName);
        var logPath = Path.Combine(logDirPath, "game.gz");

        var monitor = new AsyncSinkMonitorHook();

        if (Directory.Exists(logDirPath))
        {
            foreach (var file in Directory.GetFiles(logDirPath, "*.gz"))
            {
                try
                {
                    File.Delete(file);
                }
                catch (IOException e)
                {
                }
            }
        }

        var logger = new LoggerConfiguration()
                     .Enrich.FromLogContext()
                     .MinimumLevel.Verbose()
                     .WriteTo.Async(a =>
                     {
                         a.Console(minConsoleLevel, 
                                   "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}");
                         a.File(new CompactJsonFormatter(), logPath,
                                restrictedToMinimumLevel: minFileLevel,
                                hooks: new GZipHooks(CompressionLevel.SmallestSize),
                                retainedFileCountLimit: 5, rollOnFileSizeLimit: true, buffered: true);
                     }, monitor, 100000)
                     .CreateLogger();

        //TODO: Separate monitor logger maybe?
        monitor.SelfLog = logger;

        logger.Information("Log started for {ApplicationName}", appName);
        logger.Information("""
                           Is 64 bit: {Is64Bit}
                           Running directory: {RunningDirectory}
                           .NET version: {NetVersion}
                           """,
                        Environment.Is64BitProcess,
                        Environment.CurrentDirectory,
                        Environment.Version);

        return logger;
    }

    public static HostApplicationBuilder CreateHostBuilder(ILogger serilog)
    {
        // ReSharper disable once ContextualLoggerProblem
        TaskScheduler.UnobservedTaskException += (sender, args)
            => serilog.ForContext(sender?.GetType() ?? typeof(TaskScheduler))
                      .Fatal(args.Exception, "Unobserved task exception triggered, is observed: {Observed}",
                             args.Observed);
        
        var builder = Host.CreateApplicationBuilder(Environment.GetCommandLineArgs());
        
        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(serilog, true);

        return builder;
    }

    private static IView CreateSilkView()
    {
        IView view;
        Window.PrioritizeGlfw();
        
        var api = new GraphicsAPI(ContextAPI.None, new(1, 0));
        
        if (Window.IsViewOnly)
        {
            var opt = ViewOptions.Default;
            opt.API = api;
            view = Window.GetView(opt);
        }
        else
        {
            var opt = WindowOptions.Default;
            opt.API = api;
            opt.VSync = true;
            opt.Size = new(800, 600);
            opt.Title = "Test";

            view = Window.Create(opt);
        }

        return view;
    }

    private async Task InitializeCore()
    {
        var input = _mainView.CreateInput();
        foreach (var kb in input.Keyboards)
        {
            kb.KeyUp += (_, key, _) =>
            {
                if (key is Key.F4 && kb.IsKeyPressed(Key.AltLeft))
                {
                    _mainView.Close();
                }
            };
        }

        var eventFactory = GlobalHost.Services.GetRequiredService<EventFactory>();
        GlobalHost.Services.GetRequiredService<NativeViewManager>().MainView = new SilkView(_mainView, eventFactory);
        
        var universe = GlobalHost.Services.GetRequiredService<Universe>();
        _mainView.Update += _ => universe.Update();

        await GlobalHost.StartAsync();
        
        await universe.Initialize();
    }
}

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all necessary services to run the core
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddCoreSystemServices(this IServiceCollection services)
    {
        services
            .AddMessagePipe()
            .AddSingleton<Universe>()
            .AddSingleton<ShaderCompiler>()
            .AddSingleton<WebGpuContext>()
            .AddSingleton<NativeViewManager>()
            .AddSingleton<WebGpuRenderer>()
            .AddSingleton<WebGpuRenderSystem>()
            .AddSingleton<SystemBase, WebGpuRenderSystem>(p => p.GetRequiredService<WebGpuRenderSystem>())
            .AddSingleton<SystemBase, CameraSystem>();

        return services;
    }
}