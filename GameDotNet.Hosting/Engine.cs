using System.IO.Compression;
using GameDotNet.Core;
using GameDotNet.Core.Models;
using GameDotNet.Core.Services;
using Injectio.Attributes;
using MessagePipe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Sinks.File.GZip;

namespace GameDotNet.Hosting;

[RegisterSingleton]
public sealed class Engine
{
    public string ApplicationName { get; }

    public static HostApplicationBuilder CreateBuilder(string[] args, string appName)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = Environment.GetCommandLineArgs(),
#if Debug
            EnvironmentName = Environments.Development
#endif
            ApplicationName = appName
        });

        builder.AddServiceDefaults();

        builder.Services.AddSingleton<Engine>().AddCoreSystemServices();

        return builder;
    }

    public Engine(IHostEnvironment hostEnvironment, ILogger<Engine> logger, IServiceProvider provider)
    {
        ApplicationName = hostEnvironment.ApplicationName;

        TaskScheduler.UnobservedTaskException += (sender, args) =>
        {
            logger.LogCritical(args.Exception, "From type [{SenderType}] Unobserved task exception triggered, is observed: {Observed}", sender?.GetType(), args.Observed);
        };

        logger.LogInformation("""
                              Is 64 bit: {Is64Bit}
                              Running directory: {RunningDirectory}
                              .NET version: {NetVersion}
                              """,
                              Environment.Is64BitProcess,
                              Environment.CurrentDirectory,
                              Environment.Version);

        GlobalMessagePipe.SetProvider(provider);
    }

    //TODO: migrate from serilog to ms logging abstractions
    internal static LoggerConfiguration CreateFileLoggerConfig(string appName,
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
                                                                 a.File(new CompactJsonFormatter(),
                                                                        logPath,
                                                                        restrictedToMinimumLevel: minFileLevel,
                                                                        hooks: new GZipHooks(CompressionLevel.SmallestSize),
                                                                        retainedFileCountLimit: 5,
                                                                        rollOnFileSizeLimit: true,
                                                                        buffered: true);
                                                             },
                                                             monitor: monitor,
                                                             bufferSize: 100000);

        //TODO: Separate monitor logger maybe?
        monitor.SelfLogFactory = () => Log.Logger;

        return config;
    }
}

[RegisterSingleton<IHostedService, EngineStartupHostedService>(Duplicate = DuplicateStrategy.Append)]
internal sealed class EngineStartupHostedService(
    JobManager jobManager,
    IAsyncPublisher<EngineStartedEvent> engineStart,
    IAsyncPublisher<EngineStoppingEvent> engineStop) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken token)
    {
        await engineStart.PublishAsync(new(), AsyncPublishStrategy.Sequential, token);
        
        while (!token.IsCancellationRequested) await jobManager.Update(token);
        
        await jobManager.DisposeAsync();
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await engineStop.PublishAsync(new(), AsyncPublishStrategy.Sequential, cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}