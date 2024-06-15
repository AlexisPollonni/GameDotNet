using GameDotNet.Graphics;
using GameDotNet.Graphics.Assets.Assimp;
using GameDotNet.Graphics.WGPU;
using GameDotNet.Management;
using GameDotNet.Management.ECS;
using MessagePipe;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using Schedulers;
using Serilog;
using Serilog.Events;

namespace GameDotNet.Hosting;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all necessary services to run the core
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddCoreSystemServices(this IServiceCollection services)
    {
        services.AddMetrics()
                .AddMessagePipe()
                .AddSingleton<JobScheduler>(_ => new(new()
                {
                    MaxExpectedConcurrentJobs = 100,
                    ThreadPrefixName = "UpdateJob"
                }))
                .AddSingleton<Universe>()
                .AddSingleton<SceneManager>()
                .AddTransient<AssimpNetImporter>()
                .AddSingleton<ShaderCompiler>()
                .AddSingleton<WebGpuContext>()
                .AddSingleton<NativeViewManager>()
                .AddSingleton<WebGpuRenderer>()
                .AddSystem<WebGpuRenderSystem>()
                .AddSystem<CameraSystem>();

        return services;
    }

    public static IServiceCollection AddEngineFileLogger(this IServiceCollection services, string appName, LogEventLevel level = LogEventLevel.Verbose)
    {
        var configuration = Engine.CreateFileLoggerConfig(appName, level);
        services.AddLogging(builder => builder.AddSerilog(configuration.CreateLogger(), true));

        return services;
    }

    public static IServiceCollection AddEngineInstrumentation(this IServiceCollection services)
    {
        services.ConfigureOpenTelemetryMeterProvider(builder => builder.AddMeter("Universe.Updates"));
        return services;
    }

    public static IServiceCollection AddSystem<TSys>(this IServiceCollection services) where TSys : SystemBase
    {
        services.AddSingleton<TSys>().AddSingleton<SystemBase, TSys>(p => p.GetRequiredService<TSys>());

        return services;
    }
}