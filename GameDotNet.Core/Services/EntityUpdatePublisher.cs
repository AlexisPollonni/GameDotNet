using System.Reactive;
using Arch.Core;
using GameDotNet.Core.Abstractions;
using MessagePipe;
using Microsoft.Extensions.DependencyInjection;
using Nito.Disposables;

namespace GameDotNet.Core.Services;

file sealed class EntityUpdatePublisher : SingleDisposable<Unit>
{
    private readonly IDisposable _disposableBag;
    private readonly IPublisher<EntityCreatedEvent> _entityCreatedPublisher;
    private readonly IPublisher<EntityDestroyedEvent> _entityDestroyedPublisher;

    public EntityUpdatePublisher(
        ISubscriber<SceneInstantiatedEvent> sceneInstantiatedSubscriber,
        ISubscriber<SceneDestroyingEvent> sceneDestroyingSubscriber,
        IPublisher<EntityCreatedEvent> entityCreatedPublisher,
        IPublisher<EntityDestroyedEvent> entityDestroyedPublisher) : base(default)
    {
        _entityCreatedPublisher = entityCreatedPublisher;
        _entityDestroyedPublisher = entityDestroyedPublisher;
        _disposableBag = DisposableBag.Create(
            sceneInstantiatedSubscriber.Subscribe(OnSceneInstantiated),
            sceneDestroyingSubscriber.Subscribe(OnSceneDestroyed)
        );
    }

    private void OnSceneInstantiated(SceneInstantiatedEvent e)
    {
        var world = e.Instance.EntityWorld;

        world.SubscribeEntityCreated((in entity) => { _entityCreatedPublisher.Publish(new(entity)); });
        world.SubscribeEntityDestroyed((in entity) => { _entityDestroyedPublisher.Publish(new(entity)); });
    }

    private void OnSceneDestroyed(SceneDestroyingEvent e)
    {
        //TODO: Unsubscribe?
    }

    protected override void Dispose(Unit context)
    {
        _disposableBag.Dispose();
    }
}

file sealed class ComponentPublisher<TComponent> : SingleDisposable<Unit>
{
    private readonly IPublisher<EntityComponentAddedEvent> _componentAddedPublisher;
    private readonly IPublisher<EntityComponentSetEvent> _componentSetPublisher;
    private readonly IPublisher<EntityComponentRemovedEvent> _componentRemovedPublisher;
    private readonly IDisposable _disposables;
    
    public ComponentPublisher(
        ISubscriber<SceneInstantiatedEvent> sceneInstantiatedSubscriber,
        IPublisher<EntityComponentAddedEvent> componentAddedPublisher,
        IPublisher<EntityComponentSetEvent> componentSetPublisher,
        IPublisher<EntityComponentRemovedEvent> componentRemovedPublisher) : base(default)
    {
        _componentAddedPublisher = componentAddedPublisher;
        _componentSetPublisher = componentSetPublisher;
        _componentRemovedPublisher = componentRemovedPublisher;
        _disposables = sceneInstantiatedSubscriber.Subscribe(OnSceneInstantiated);
    }

    private void OnSceneInstantiated(SceneInstantiatedEvent e)
    {
        var world = e.Instance.EntityWorld;
        
        world.SubscribeComponentAdded<TComponent>((in entity, ref comp) =>
        {
            _componentAddedPublisher.Publish(
                new(entity, typeof(TComponent)));
        });

        world.SubscribeComponentSet<TComponent>((in entity, ref comp) =>
        {
            _componentSetPublisher.Publish(
                new(entity, typeof(TComponent)));
        });

        world.SubscribeComponentRemoved<TComponent>((in entity, ref comp) =>
        {
            _componentRemovedPublisher.Publish(
                new(entity, typeof(TComponent)));
        });
    }

    protected override void Dispose(Unit context)
    {
        _disposables.Dispose();
    }
}

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