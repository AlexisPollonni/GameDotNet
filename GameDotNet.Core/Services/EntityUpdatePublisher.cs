using Arch.Core;
using GameDotNet.Core.Abstractions;
using MessagePipe;
using Microsoft.Extensions.DependencyInjection;

namespace GameDotNet.Core.Services;

public static class ServiceCollectionExtensions
{
    private class EntityUpdatePublisher
    {
        public EntityUpdatePublisher(SceneManager scene,
                                     IPublisher<EntityCreatedEvent> entityCreatedPublisher,
                                     IPublisher<EntityDestroyedEvent> entityDestroyedPublisher)
        {
            scene.World.SubscribeEntityCreated((in entity) => { entityCreatedPublisher.Publish(new(entity)); });

            scene.World.SubscribeEntityDestroyed((in entity) => { entityDestroyedPublisher.Publish(new(entity)); });
        }
    }

    private class ComponentPublisher<TComponent>
    {
        public ComponentPublisher(SceneManager sceneManager,
                                  IPublisher<EntityComponentAddedEvent> componentAddedPublisher,
                                  IPublisher<EntityComponentSetEvent> componentSetPublisher,
                                  IPublisher<EntityComponentRemovedEvent> componentRemovedPublisher)
        {
            sceneManager.World.SubscribeComponentAdded<TComponent>((in entity, ref comp) =>
            {
                componentAddedPublisher.Publish(
                    new(entity, typeof(TComponent)));
            });

            sceneManager.World.SubscribeComponentSet<TComponent>((in entity, ref comp) =>
            {
                componentSetPublisher.Publish(
                    new(entity, typeof(TComponent)));
            });

            sceneManager.World.SubscribeComponentRemoved<TComponent>((in entity, ref comp) =>
            {
                componentRemovedPublisher.Publish(
                    new(entity, typeof(TComponent)));
            });
        }
    }

    /// <summary>
    /// Registers an ECS component type if not already registered in the singleton registry and additionally registers
    /// publishers to notify when components of this type are added, set or removed from entities.
    /// </summary>
    /// <typeparam name="T">Ecs component type to register</typeparam>
    /// <returns></returns>
    public static IServiceCollection AddEcsComponent<T>(this IServiceCollection services)
    {
        if (ComponentRegistry.Has<T>()) return services;

        ComponentRegistry.Add<T>();

        services.TryAddActivatedSingleton<EntityUpdatePublisher>();
        services.TryAddActivatedSingleton<ComponentPublisher<T>>();

        return services;
    }
}