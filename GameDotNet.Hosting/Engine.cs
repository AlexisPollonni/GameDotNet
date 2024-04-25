using System.IO.Compression;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using GameDotNet.Core;
using GameDotNet.Core.Tools.Extensions;
using GameDotNet.Management.ECS;
using MessagePipe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Sinks.File.GZip;
using ILogger = Serilog.ILogger;

namespace GameDotNet.Hosting;

public sealed class Engine : IDisposable
{
    public IScheduler MainScheduler { get; }
    public HostApplicationBuilder Builder { get; }
    public IHost? GlobalHost { get; private set; }

    public IObservable<IHost> OnInitialized => _initSubject.ObserveOn(MainScheduler);

    private bool _initialized;
    private bool _running;
    private readonly AsyncSubject<IHost> _initSubject;
    private AsyncSubject<Unit>? _update;

    public Engine(ILogger logger, IScheduler? mainScheduler = null)
    {
        _initSubject = new();
        MainScheduler = mainScheduler ?? new EventLoopScheduler();
        Builder = CreateHostBuilder(logger);
    }

    private async Task Initialize(CancellationToken token = default)
    {
        GlobalHost = Builder.Build();

        GlobalMessagePipe.SetProvider(GlobalHost.Services);
        var universe = GlobalHost.Services.GetRequiredService<Universe>();

        await MainScheduler.StartAsync(universe.Initialize, token);

        if (!token.IsCancellationRequested)
        {
            _initialized = true;
            _initSubject.OnNext(GlobalHost);
            _initSubject.OnCompleted();
        }
    }

    public async Task Start(CancellationToken token = default)
    {
        if (_running) return;
        if (!_initialized) await Initialize(token);
        if (token.IsCancellationRequested) return;

        _running = true;

        await GlobalHost!.StartAsync(token);

        var universe = GlobalHost.Services.GetRequiredService<Universe>();

        _update = Observable.Repeat((universe, this), MainScheduler)
                  .TakeUntil(tuple => !tuple.Item2._running)
                  .Select(tuple =>
                  {
                      tuple.universe.Update();
                      return Unit.Default;
                  })
                  .RunAsync(token);

        if (token.IsCancellationRequested) _running = false;
    }

    public async Task Stop(CancellationToken token = default)
    {
        if (!_running) return;
        _running = false;

        if (GlobalHost is not null)
        {
            await MainScheduler.StartAsync(() =>
            {
                var u = GlobalHost.Services.GetRequiredService<Universe>();
                u.Dispose(); //Dispose the Universe safely on the main thread before the GlobalHost attempts to
            }, token);
            
            await GlobalHost.StopAsync(token);
        }
    }

    public void Dispose()
    {
        GlobalHost?.Dispose();
        _update?.Dispose();
        _initSubject.Dispose();

        if (MainScheduler is EventLoopScheduler els) els.Dispose();
    }

    public static LoggerConfiguration CreateLoggerConfig(string appName,
                                                         LogEventLevel minConsoleLevel = LogEventLevel.Debug,
                                                         LogEventLevel minFileLevel = LogEventLevel.Verbose)
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
                catch (IOException e) { }
            }
        }

        var config = new LoggerConfiguration().Enrich.FromLogContext()
                                              .MinimumLevel.Verbose()
                                              .WriteTo.Async(a =>
                                                             {
                                                                 a.Console(minConsoleLevel,
                                                                           "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}");
                                                                 a.File(new CompactJsonFormatter(),
                                                                        logPath,
                                                                        restrictedToMinimumLevel: minFileLevel,
                                                                        hooks: new GZipHooks(CompressionLevel.SmallestSize),
                                                                        retainedFileCountLimit: 5,
                                                                        rollOnFileSizeLimit: true,
                                                                        buffered: true);
                                                             },
                                                             monitor,
                                                             100000);

        //TODO: Separate monitor logger maybe?
        monitor.SelfLogFactory = () => Log.Logger;

        return config;
    }

    public static ILogger CreateLogger(LoggerConfiguration configuration)
    {
        var logger = configuration.CreateLogger();

        logger.Information("Log started for GameDotNet Engine");
        logger.Information("""
                           Is 64 bit: {Is64Bit}
                           Running directory: {RunningDirectory}
                           .NET version: {NetVersion}
                           """,
                           Environment.Is64BitProcess,
                           Environment.CurrentDirectory,
                           Environment.Version);

        Log.Logger = logger;

        return logger;
    }

    private HostApplicationBuilder CreateHostBuilder(ILogger serilog)
    {
        // ReSharper disable once ContextualLoggerProblem
        TaskScheduler.UnobservedTaskException += (sender, args) =>
            serilog.ForContext(sender?.GetType() ?? typeof(TaskScheduler))
                   .Fatal(args.Exception, "Unobserved task exception triggered, is observed: {Observed}", args.Observed);

        var builder = Host.CreateApplicationBuilder(Environment.GetCommandLineArgs());

        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(serilog, true);

        builder.Services
               .AddSingleton(this)
               .AddCoreSystemServices();

        return builder;
    }
}