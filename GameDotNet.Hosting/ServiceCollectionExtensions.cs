using GameDotNet.Core.Abstractions;
using GameDotNet.Core.Tooling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
                    .AddGameDotNetCore()
                    .AddGameDotNetGraphics()
                    .AddGameDotNetHosting()
                    .AddPooled<PooledValueTaskSource>();//TODO: move this to core registrations
        }

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


/// <summary>
/// Generates service collection registrations for frame jobs and job dependencies
/// TODO: in the future will be included in an analyzer/source generator referenced by scripting projects as well as the engine.
/// TODO: In future will include registration for other types of services (event based, etc...)
/// </summary>
public static partial class GeneratedServiceCollectionExtensions
{
    [GenerateServiceRegistrations(AssignableTo = typeof(IUpdateJob), AssemblyNameFilter = "GameDotNet*", Lifetime = ServiceLifetime.Singleton)]
    public static partial IServiceCollection AddUpdateJobs(this IServiceCollection services);
    
    [GenerateServiceRegistrations(AssignableTo = typeof(IJobDependsOn<>),
                                  AssemblyNameFilter = "GameDotNet*",
                                  CustomHandler = nameof(Core.Services.ServiceCollectionExtensions.RegisterJobAndDependency))]
    public static partial IServiceCollection AddJobDependencies(this IServiceCollection services);
}