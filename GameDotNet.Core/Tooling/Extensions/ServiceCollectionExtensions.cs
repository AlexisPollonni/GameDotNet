using Arch.Core;
using GameDotNet.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GameDotNet.Core.Tooling.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers an ECS component type if not already registered in the singleton registry and additionally registers
    /// publishers to notify when components of this type are added, set or removed from entities.
    /// </summary>
    /// <typeparam name="T">Ecs component type to register</typeparam>
    /// <returns></returns>
    public static IServiceCollection AddEcsComponent<T>(this IServiceCollection services)
    {
        services.TryAddActivatedSingleton<EntityUpdatePublisher>();
        services.TryAddActivatedSingleton<ComponentPublisher<T>>();

        ComponentRegistry.Add<T>();

        return services;
    }
}