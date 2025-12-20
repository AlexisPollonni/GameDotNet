using Arch.Core;
using GameDotNet.Core.Abstractions;
using GameDotNet.Core.Tooling;
using MessagePipe;
using Microsoft.Extensions.DependencyInjection;
using Nito.Disposables;

namespace GameDotNet.Core.Services;

internal sealed class EntityUpdatePublisher : SingleDisposable<EmptyStruct>
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

    protected override void Dispose(EmptyStruct context)
    {
        _disposableBag.Dispose();
    }
}

internal sealed class ComponentPublisher<TComponent> : SingleDisposable<EmptyStruct>
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

    protected override void Dispose(EmptyStruct context)
    {
        _disposables.Dispose();
    }
}