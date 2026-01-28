using GameDotNet.Core.Abstractions;
using MessagePipe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shouldly;
using ValueTaskSupplement;
using static GameDotNet.Core.Abstractions.IEventRegistry;

namespace GameDotNet.Core.Services;

[RegisterSingleton<IHostedService>]
[RegisterSingleton<IEventRegistry>]
[RegisterSingleton<IEventBus>]
internal class EventRegistry(
    ILogger<EventRegistry> logger,
    TimeProvider timeProvider,
    IServiceProvider serviceProvider,
    IZeroAllocThreadPoolScheduler<EventRegistry.SubscriptionWorkItem> scheduler)
    : BackgroundService, IEventRegistry, IAsyncDisposable,

        //implements publisher in same class for simplicity
        IEventBus
{
    private readonly List<ValueTask> _listenerTasks = [];
    private readonly Lock _listenersLock = new();
    
    [RegisterServices]
    internal static void RegisterDependencies(IServiceCollection services)
    {
        services.AddPooled<ResettableWorkItem<SubscriptionWorkItem>>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stoppingToken.ThrowIfCancellationRequested();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await WaitUntilListenerAvailable(stoppingToken);

                ValueTask[] activeTasks;
                lock (_listenersLock)
                {
                    activeTasks = _listenerTasks.ToArray();
                }

                var completedTaskIndex = await ValueTaskEx.WhenAny(activeTasks);

                ValueTask completedTask;
                lock (_listenersLock)
                {
                    completedTask = _listenerTasks[completedTaskIndex];
                    _listenerTasks.RemoveAt(completedTaskIndex);
                }

                // Check if the task completed normally or with error
                await completedTask;
            }
            catch (OperationCanceledException)
            {
                logger.LogDebug("Event listener task was cancelled");
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Event listener task faulted");
            }

            lock (_listenersLock)
            {
                logger.LogDebug("Active listeners: {Count}", _listenerTasks.Count);
            }
        }

        return;

        async ValueTask WaitUntilListenerAvailable(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                lock (_listenersLock)
                {
                    if (_listenerTasks.Count > 0)
                    {
                        break;
                    }
                }

                logger.LogDebug("No event listeners registered yet, waiting...");
                await Task.Delay(TimeSpan.FromMilliseconds(100), timeProvider, token);
            }
        }
    }


    // ===== IEventRegistry Implementation =====

    public EventSubscriptionBuilder On<TEvent>(
        EventRegistrationHandler<TEvent> handler, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var task = QueueSubscriber(handler, token);

        lock (_listenersLock)
        {
            _listenerTasks.Add(task);
        }

        return default;
    }

    public EventSubscriptionBuilder On<TKey, TEvent>(TKey key,
        EventRegistrationHandler<TEvent> handler, CancellationToken token = default)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(key);

        var task = QueueSubscriber(key, handler, token);

        lock (_listenersLock)
        {
            _listenerTasks.Add(task);
        }

        return default; // ref struct, no actual state needed
    }

    private async ValueTask QueueSubscriber<TKey, TEvent>(TKey key, EventRegistrationHandler<TEvent> handler,
        CancellationToken token) where TKey : notnull
    {
        var subscriber = serviceProvider.GetRequiredService<ISingletonAsyncSubscriber<TKey, TEvent>>();
        var enumerable = subscriber.AsAsyncEnumerable(key);

        try
        {
            await handler(enumerable, token);
        }
        catch (OperationCanceledException)
        {
            serviceProvider.GetService<ILogger<EventRegistry>>()?.LogDebug(
                "Event listener for {EventType} with key {Key} was cancelled",
                typeof(TEvent).Name, key);
        }
        catch (Exception ex)
        {
            serviceProvider.GetService<ILogger<EventRegistry>>()?.LogError(ex,
                "Event listener for {EventType} with key {Key} faulted",
                typeof(TEvent).Name, key);
        }
    }

    private async ValueTask QueueSubscriber<TEvent>(EventRegistrationHandler<TEvent> handler,
        CancellationToken token)
    {
        var subscriber = serviceProvider.GetRequiredService<ISingletonAsyncSubscriber<TEvent>>();
        var enumerable = subscriber.AsAsyncEnumerable();

        try
        {
            await handler(enumerable, token);
        }
        catch (OperationCanceledException)
        {
            serviceProvider.GetService<ILogger<EventRegistry>>()?.LogDebug(
                "Event listener for {EventType} was cancelled",
                typeof(TEvent).Name);
        }
        catch (Exception ex)
        {
            serviceProvider.GetService<ILogger<EventRegistry>>()?.LogError(ex,
                "Event listener for {EventType} faulted",
                typeof(TEvent).Name);
        }
    }

    internal readonly record struct SubscriptionWorkItem(IServiceProvider Provider, object? Key, object Handler);

    // ===== IEventBus Implementation =====
    //TODO: Consider caching publishers for performance if profiling show it's needed

    public void Publish<TEvent>(TEvent evt)
    {
        var publisher = serviceProvider.GetRequiredService<ISingletonAsyncPublisher<TEvent>>();

        publisher.Publish(evt);
    }

    public void Publish<TKey, TEvent>(TKey key, TEvent evt) where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(key);

        var publisher = serviceProvider.GetRequiredService<ISingletonAsyncPublisher<TKey, TEvent>>();

        publisher.Publish(key, evt);
    }

    public async ValueTask PublishAsync<TEvent>(TEvent evt, CancellationToken cancellationToken = default)
    {
        var publisher = serviceProvider.GetRequiredService<ISingletonAsyncPublisher<TEvent>>();
        await publisher.PublishAsync(evt, cancellationToken);
    }

    public async ValueTask PublishAsync<TKey, TEvent>(TKey key, TEvent evt,
        CancellationToken cancellationToken = default) where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(key);

        var publisher = serviceProvider.GetRequiredService<ISingletonAsyncPublisher<TKey, TEvent>>();
        await publisher.PublishAsync(key, evt, cancellationToken);
    }

    public async ValueTask PublishAllAsync<TEvent>(IAsyncEnumerable<TEvent> events,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(events);

        var publisher = serviceProvider.GetRequiredService<ISingletonAsyncPublisher<TEvent>>();

        await foreach (var evt in events.WithCancellation(cancellationToken))
        {
            await publisher.PublishAsync(evt, cancellationToken);
        }
    }

    public async ValueTask PublishAllAsync<TKey, TEvent>(TKey key, IAsyncEnumerable<TEvent> events,
        CancellationToken cancellationToken = default) where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(events);

        var publisher = serviceProvider.GetRequiredService<ISingletonAsyncPublisher<TKey, TEvent>>();

        await foreach (var evt in events.WithCancellation(cancellationToken))
        {
            await publisher.PublishAsync(key, evt, cancellationToken);
        }
    }

    // ===== Channel Management =====

    public async ValueTask DisposeAsync()
    {
        lock (_listenerTasks)
        {
            if (_listenerTasks.Count == 0)
            {
                return;
            }
        }
        // Wait for all remaining tasks with a timeout

        try
        {
            Task listenerCleanupTask;

            lock (_listenersLock)
            {
                listenerCleanupTask = Task.WhenAll(_listenerTasks.Select(task => task.AsTask()))
                    .WaitAsync(TimeSpan.FromSeconds(5), timeProvider);
            }

            await listenerCleanupTask;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Shutdown cancelled, some listeners may not have completed cleanly");
        }
        catch (TimeoutException)
        {
            logger.LogWarning("Timeout waiting for listeners to complete during shutdown");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error waiting for listener tasks during shutdown");
        }
    }
}