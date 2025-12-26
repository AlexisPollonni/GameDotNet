using Arch.Core;
using GameDotNet.Core.Abstractions;
using GameDotNet.Core.Tooling;
using GameDotNet.Core.Tooling.Extensions;
using Nito.Disposables;

namespace GameDotNet.Core.Services;

[RegisterSingleton<IEventListener>(Duplicate = DuplicateStrategy.Append)]
internal sealed class EntityUpdatePublisher(IEventBus eventBus)
    : SingleAsyncDisposable<EmptyStruct>(default), IEventListener
{
    private readonly CancellationTokenSource _subscriptionTokenSource = new();


    public void Configure(IEventRegistry registry)
    {
        registry.OnEvent<SceneInstantiatedEvent>(OnSceneInstantiated, _subscriptionTokenSource.Token);
        registry.OnEvent<SceneDestroyingEvent>(OnSceneDestroying, _subscriptionTokenSource.Token);
    }

    private ValueTask OnSceneInstantiated(SceneInstantiatedEvent sceneEvent, CancellationToken cancellationToken)
    {
        var world = sceneEvent.Instance.EntityWorld;

        world.SubscribeEntityCreated(OnEntityCreated);
        world.SubscribeEntityDestroyed(OnEntityDestroyed);
        
        return ValueTask.CompletedTask;
    }

    private static ValueTask OnSceneDestroying(SceneDestroyingEvent sceneDestroyingEvent, CancellationToken cancellationToken)
    {
        //TODO: Unsubscribe?
        return default;
    }

    private void OnEntityCreated(in Entity entity)
    {
        eventBus.Publish(new EntityCreatedEvent(entity));
    }

    private void OnEntityDestroyed(in Entity entity)
    {
        eventBus.Publish(new EntityDestroyedEvent(entity));
    }

    protected override async ValueTask DisposeAsync(EmptyStruct context)
    {
        await _subscriptionTokenSource.CancelAsync();
        _subscriptionTokenSource.Dispose();
    }
}

internal sealed class ComponentPublisher<TComponent>(IEventBus eventBus) : SingleAsyncDisposable<EmptyStruct>(default), IEventListener
{
    private readonly CancellationTokenSource _subscriptionTokenSource = new();

    public void Configure(IEventRegistry registry)
    {
        registry.OnEvent<SceneInstantiatedEvent>(OnSceneInstantiated, _subscriptionTokenSource.Token);
    }

    private ValueTask OnSceneInstantiated(SceneInstantiatedEvent e, CancellationToken cancellationToken)
    {
        var world = e.Instance.EntityWorld;

        world.SubscribeComponentAdded<TComponent>((in entity, ref comp) =>
        {
            eventBus.Publish(
                new EntityComponentAddedEvent(entity, typeof(TComponent)));
        });

        world.SubscribeComponentSet<TComponent>((in entity, ref comp) =>
        {
            eventBus.Publish(
                new EntityComponentSetEvent(entity, typeof(TComponent)));
        });

        world.SubscribeComponentRemoved<TComponent>((in entity, ref comp) =>
        {
            eventBus.Publish(
                new EntityComponentRemovedEvent(entity, typeof(TComponent)));
        });
        return ValueTask.CompletedTask;
    }

    protected override async ValueTask DisposeAsync(EmptyStruct context)
    {
        await _subscriptionTokenSource.CancelAsync();
        _subscriptionTokenSource.Dispose();
    }
}