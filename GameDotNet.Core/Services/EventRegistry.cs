using System.Collections.Concurrent;
using System.Threading.Channels;
using GameDotNet.Core.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vogen;

namespace GameDotNet.Core.Services;

/// <summary>
/// Dummy key type used for global (non-keyed) events
/// </summary>
file readonly struct GlobalEventKey : IEquatable<GlobalEventKey>
{
    public static readonly GlobalEventKey Instance = default;

    public bool Equals(GlobalEventKey other) => true;
    public override bool Equals(object? obj) => obj is GlobalEventKey;
    public override int GetHashCode() => 0;
}

[ValueObject<ValueTuple<Type, object>>]
internal readonly partial struct ChannelKey
{
    public static ChannelKey From<TKey, TEvent>(TKey key) where TKey : notnull
    {
        return From((typeof(TEvent), key));
    }
    
    public static ChannelKey FromGlobal<TEvent>()
    {
        return From<GlobalEventKey, TEvent>(GlobalEventKey.Instance);
    }
}

internal class EventRegistry(ILogger<EventRegistry> logger, IEnumerable<IEventListener> listeners, TimeProvider timeProvider)
    : BackgroundService, IEventRegistry, IAsyncDisposable,

        //implements publisher in same class for simplicity
        IEventBus
{
    // Unified channel storage - nested dictionary: Type -> Key -> Channel
    // Global events use GlobalEventKey.Instance as the key
    private readonly Dictionary<ChannelKey, object> _channels = new();

    // Store cleanup actions to avoid reflection during shutdown
    private readonly ConcurrentDictionary<ChannelKey, Action> _cleanupActions = new();

    private readonly Lock _channelsLock = new();

    private readonly List<Task> _listenerTasks = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ConfigureStartupListeners();

        stoppingToken.ThrowIfCancellationRequested();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await WaitUntilListenerAvailable(stoppingToken);

                var completedTask = await Task.WhenAny(_listenerTasks);
                _listenerTasks.Remove(completedTask);

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

            logger.LogDebug("Active listeners: {Count}", _listenerTasks.Count);
        }

        return;

        void ConfigureStartupListeners()
        {
            // Configure all registered event listeners at startup
            foreach (var listener in listeners)
            {
                try
                {
                    listener.Configure(this);
                    logger.LogDebug("Configured event listener: {ListenerType}", listener.GetType().Name);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to configure event listener: {ListenerType}", listener.GetType().Name);
                }
            }
        }

        async ValueTask WaitUntilListenerAvailable(CancellationToken token)
        {
            while (_listenerTasks.Count == 0)
            {
                token.ThrowIfCancellationRequested();
                logger.LogWarning("No event listeners registered yet, waiting...");
                await Task.Delay(TimeSpan.FromMilliseconds(100), token);
            }
        }
    }


    // ===== IEventRegistry Implementation =====

    public IEventRegistry.EventSubscriptionBuilder On<TEvent>(Func<IAsyncEnumerable<TEvent>, CancellationToken, Task> handler, CancellationToken token = default)
    {
        return On(GlobalEventKey.Instance, handler, token);
    }

    public IEventRegistry.EventSubscriptionBuilder On<TKey, TEvent>(TKey key,
        Func<IAsyncEnumerable<TEvent>, CancellationToken, Task> handler, CancellationToken token = default) where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(key);

        var channel = GetOrCreateChannel<TKey, TEvent>(key);
        var stream = channel.Reader.ReadAllAsync(token);

        // Start the handler task
        var listenerTask = Task.Run(async Task? () =>
        {
            try
            {
                await handler(stream, token);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception in keyed event handler for {EventType} with key {Key}",
                    typeof(TEvent).Name, key);
            }
        }, token);

        _listenerTasks.Add(listenerTask);

        return default; // ref struct, no actual state needed
    }

    // ===== IEventBus Implementation =====

    public void Publish<TEvent>(TEvent evt)
    {
        Publish(GlobalEventKey.Instance, evt);
    }

    public void Publish<TKey, TEvent>(TKey key, TEvent evt) where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(key);

        var channel = GetOrCreateChannel<TKey, TEvent>(key);
        
        // Use TryWrite for lock-free, zero-allocation fast path
        if (!channel.Writer.TryWrite(evt))
        {
            // Fallback to sync write if channel is bounded and full
            channel.Writer.WriteAsync(evt).GetAwaiter().GetResult();
        }
    }

    public ValueTask PublishAsync<TEvent>(TEvent evt, CancellationToken cancellationToken = default)
    {
        return PublishAsync(GlobalEventKey.Instance, evt, cancellationToken);
    }

    public async ValueTask PublishAsync<TKey, TEvent>(TKey key, TEvent evt,
        CancellationToken cancellationToken = default) where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(key);

        var channel = GetOrCreateChannel<TKey, TEvent>(key);
        await channel.Writer.WriteAsync(evt, cancellationToken);
    }

    public ValueTask PublishAllAsync<TEvent>(IAsyncEnumerable<TEvent> events,
        CancellationToken cancellationToken = default)
    {
        return PublishAllAsync(GlobalEventKey.Instance, events, cancellationToken);
    }

    public async ValueTask PublishAllAsync<TKey, TEvent>(TKey key, IAsyncEnumerable<TEvent> events,
        CancellationToken cancellationToken = default) where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(events);

        var channel = GetOrCreateChannel<TKey, TEvent>(key);

        await foreach (var evt in events.WithCancellation(cancellationToken))
        {
            await channel.Writer.WriteAsync(evt, cancellationToken);
        }
    }

    // ===== Channel Management =====

    private Channel<TEvent> GetOrCreateChannel<TKey, TEvent>(TKey key) where TKey : notnull
    {
        var eventType = typeof(TEvent);

        // Get or create the type-level dictionary
        Dictionary<TKey, Channel<TEvent>> keyDict;
        
        lock (_channelsLock)
        {
            if (!_channels.TryGetValue(eventType, out var existingDict))
            {
                keyDict = new();
                _channels[eventType] = keyDict;

                // Register cleanup action for this event type (no reflection!)
                _cleanupActions[eventType] = () => CompleteAllChannelsForEventType(keyDict);
            }
            else
            {
                keyDict = (Dictionary<TKey, Channel<TEvent>>)existingDict;
            }
        }

        // Now work with the key-level dictionary
        lock (keyDict)
        {
            if (keyDict.TryGetValue(key, out var channel))
            {
                return channel;
            }

            channel = Channel.CreateUnbounded<TEvent>(new()
            {
                SingleReader = false,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });

            keyDict[key] = channel;

            if (typeof(TKey) == typeof(GlobalEventKey))
            {
                logger.LogDebug("Created global event channel for {EventType}", eventType.Name);
            }
            else
            {
                logger.LogDebug("Created keyed event channel for {EventType} with key {Key}", eventType.Name, key);
            }

            return channel;
        }
    }

    private static void CompleteAllChannelsForEventType<TKey, TEvent>(Dictionary<TKey, Channel<TEvent>> keyDict)
        where TKey : notnull
    {
        foreach (var channel in keyDict.Values)
        {
            channel.Writer.TryComplete();
        }
    }

    public async ValueTask DisposeAsync()
    {
        // Complete all channels to signal no more events
        lock (_channelsLock)
        {
            foreach (var cleanupAction in _cleanupActions.Values)
            {
                cleanupAction();
            }
        }
        
        // Wait for all remaining tasks with a timeout
        if (_listenerTasks.Count > 0)
        {
            try
            {
                await Task.WhenAll(_listenerTasks).WaitAsync(TimeSpan.FromSeconds(5), timeProvider);
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
}