using GameDotNet.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GameDotNet.Core.Services;

[RegisterSingleton<IJobDependencyGraph>]
internal class DefaultJobDependencyGraph(IServiceProvider provider) : IJobDependencyGraph
{
    internal record JobDependencyDescriptor(Type JobType, Type JobDependency);

    public IEnumerable<Type> GetDependencies(Type jobType)
    {
        return provider.GetKeyedServices<JobDependencyDescriptor>(jobType)
            .Select(descriptor => descriptor.JobDependency);
    }
}

public static class ServiceCollectionExtensions
{
    public static void RegisterJobAndDependency<TJob, TJobDependency>(IServiceCollection services)
        where TJob : class, IJobDependsOn<TJobDependency>, IUpdateJob where TJobDependency : IUpdateJob
    {
        services.TryAddSingleton<IJobDependencyGraph, DefaultJobDependencyGraph>();
        services.TryAddKeyedSingleton(serviceKey: typeof(TJob),
            new DefaultJobDependencyGraph.JobDependencyDescriptor(
                typeof(TJob),
                typeof(TJobDependency)));

        services.TryAddSingleton<IUpdateJob, TJob>();
    }
}