using GameDotNet.Core;
using GameDotNet.Core.Abstractions;
using GameDotNet.Core.Services;
using GameDotNet.Core.Tooling;
using GameDotNet.Graphics;
using GameDotNet.Graphics.Assets.Assimp;
using GameDotNet.Graphics.WGPU;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using Serilog;
using Serilog.Events;
using ServiceScan.SourceGenerator;

namespace GameDotNet.Hosting;

public static class ServiceCollectionExtensions
{
    public static IHostApplicationBuilder AddEngineFileLogger(this IHostApplicationBuilder builder,
                                                              LogEventLevel level = LogEventLevel.Verbose)
    {
        var configuration = Engine.CreateFileLoggerConfig(builder.Environment.ApplicationName, level);
        builder.Logging.AddSerilog(configuration.CreateLogger(), true);

        return builder;
    }

    /// <param name="services"></param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers all necessary services to run the core
        /// </summary>
        /// <returns></returns>
        public IServiceCollection AddCoreSystemServices()
        {
            return services.AddMetrics()
                    .AddMessagePipe().Services
                    .AddSingleton<JobManager>()
                    .AddSingleton<SceneManager>()
                    .AddTransient<AssimpNetImporter>()
                    .AddSingleton<SlangContext>(p => new(p.GetRequiredService<ILogger<SlangContext>>(), ["Assets/"]))
                    .AddSingleton<WebGpuRenderer>()
                    
                    .AddPooled<PooledValueTaskSource>();
        }

        public IServiceCollection AddEngineHostedService() =>
            services.AddHostedService<EngineStartupHostedService>();

        public IServiceCollection AddEngineInstrumentation()
        {
            const string universeMeterName = "Universe.Updates";
            var boundaries = Enumerable.Range(0, 10000).Select(i => i / 100D).ToArray();

            services.ConfigureOpenTelemetryMeterProvider(builder =>
            {
                builder.AddMeter(universeMeterName)
                       //Change to exponential histogram view when https://github.com/dotnet/aspire/issues/4381 is fixed
                       .AddView(instrument => instrument.Meter.Name == universeMeterName
                                    ? new ExplicitBucketHistogramConfiguration
                                    {
                                        Boundaries = boundaries
                                    }
                                    : null);
            });


            return services;
        }
    }
}

public static partial class GeneratedServiceCollectionExtensions
{
    [GenerateServiceRegistrations(AssignableTo = typeof(IUpdateJob), AssemblyNameFilter = "GameDotNet*", Lifetime = ServiceLifetime.Singleton)]
    public static partial IServiceCollection AddUpdateJobs(this IServiceCollection services);
    
    [GenerateServiceRegistrations(AssignableTo = typeof(IJobDependsOn<>),
                                  AssemblyNameFilter = "GameDotNet*",
                                  CustomHandler = nameof(RegisterJobAndDependency))]
    public static partial IServiceCollection AddJobDependencies(this IServiceCollection services);

    private static void RegisterJobAndDependency<TJob, TJobDependency>(IServiceCollection services)
        where TJob : class, IJobDependsOn<TJobDependency>, IUpdateJob where TJobDependency : IUpdateJob
    {
        services.TryAddSingleton<IJobDependencyGraph, DefaultJobDependencyGraph>();
        services.AddKeyedSingleton(serviceKey: typeof(TJob),
                                   new DefaultJobDependencyGraph.JobDependencyDescriptor(
                                       typeof(TJob),
                                       typeof(TJobDependency)));

        services.TryAddSingleton<IUpdateJob, TJob>();
    }

    private class DefaultJobDependencyGraph(IServiceProvider provider) : IJobDependencyGraph
    {
        internal record JobDependencyDescriptor(Type JobType, Type JobDependency);

        public IEnumerable<Type> GetDependencies(Type jobType)
        {
            return provider.GetKeyedServices<JobDependencyDescriptor>(jobType)
                           .Select(descriptor => descriptor.JobDependency);
        }
    }
}