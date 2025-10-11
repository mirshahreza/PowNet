using System.Collections.Concurrent;
using System.Reflection;
using PowNet.Logging;

namespace PowNet.Extensions
{
    /// <summary>
    /// Event-driven architecture extensions for PowNet framework
    /// </summary>
    public static class EventBusExtensions
    {
        private static readonly Logger _logger = PowNetLogger.GetLogger("EventBus");
        private static readonly ConcurrentDictionary<Type, List<IEventHandler>> _handlers = new();
        private static readonly ConcurrentDictionary<string, List<IEventHandler>> _namedHandlers = new();
        private static readonly EventBusOptions _options = new();

        public static void Reset()
        {
            _handlers.Clear();
            _namedHandlers.Clear();
            _options.UseParallelExecution = true;
            _options.ContinueOnHandlerError = true;
            _options.MaxConcurrentHandlers = Environment.ProcessorCount * 2;
        }

        #region Event Publishing

        /// <summary>
        /// Publish event to all registered handlers
        /// </summary>
        public static async Task PublishAsync<TEvent>(this TEvent eventData, 
            CancellationToken cancellationToken = default) where TEvent : class
        {
            if (eventData == null)
                throw new ArgumentNullException(nameof(eventData));

            var eventType = typeof(TEvent);
            var eventName = eventType.Name;

            _logger.LogDebug("Publishing event: {EventType}", eventName);

            var tasks = new List<Task>();

            // Handle typed handlers
            if (_handlers.TryGetValue(eventType, out var typedHandlers))
            {
                foreach (var handler in typedHandlers)
                {
                    tasks.Add(ExecuteHandlerSafely(handler, eventData, cancellationToken));
                }
            }

            // Handle named handlers
            if (_namedHandlers.TryGetValue(eventName, out var namedHandlers))
            {
                foreach (var handler in namedHandlers)
                {
                    tasks.Add(ExecuteHandlerSafely(handler, eventData, cancellationToken));
                }
            }

            if (_options.UseParallelExecution && tasks.Count > 1)
            {
                await Task.WhenAll(tasks);
            }
            else
            {
                foreach (var task in tasks)
                {
                    await task;
                }
            }

            _logger.LogDebug("Event published successfully: {EventType}, Handlers: {HandlerCount}", 
                eventName, tasks.Count);
        }

        /// <summary>
        /// Publish event with retry logic
        /// </summary>
        public static async Task PublishWithRetryAsync<TEvent>(this TEvent eventData,
            int maxRetries = 3,
            TimeSpan? delay = null,
            CancellationToken cancellationToken = default) where TEvent : class
        {
            delay ??= TimeSpan.FromMilliseconds(100);
            Exception? lastException = null;

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    await eventData.PublishAsync(cancellationToken);
                    return;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    if (attempt == maxRetries)
                    {
                        _logger.LogError("Event publishing failed after {MaxRetries} attempts: {Error}", 
                            maxRetries + 1, ex.Message);
                        throw;
                    }

                    _logger.LogWarning("Event publishing failed on attempt {Attempt}, retrying in {Delay}ms", 
                        attempt + 1, delay.Value.TotalMilliseconds);

                    await Task.Delay(delay.Value, cancellationToken);
                    delay = TimeSpan.FromTicks(delay.Value.Ticks * 2); // Exponential backoff
                }
            }

            throw lastException!;
        }

        /// <summary>
        /// Schedule event for future publishing
        /// </summary>
        public static void ScheduleEvent<TEvent>(this TEvent eventData, DateTime publishAt) where TEvent : class
        {
            var delay = publishAt - DateTime.UtcNow;
            if (delay <= TimeSpan.Zero)
            {
                _ = Task.Run(() => eventData.PublishAsync());
                return;
            }

            _ = Task.Delay(delay).ContinueWith(async _ =>
            {
                try
                {
                    await eventData.PublishAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogException(ex, "Scheduled event publishing failed");
                }
            });
        }

        #endregion

        #region Event Subscription

        /// <summary>
        /// Subscribe to events with typed handler
        /// </summary>
        public static EventSubscription Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class
        {
            return Subscribe<TEvent>(async (evt, ct) =>
            {
                handler(evt);
                await Task.CompletedTask;
            });
        }

        /// <summary>
        /// Subscribe to events with async typed handler
        /// </summary>
        public static EventSubscription Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler) 
            where TEvent : class
        {
            var eventHandler = new TypedEventHandler<TEvent>(handler);
            var eventType = typeof(TEvent);

            _handlers.AddOrUpdate(eventType,
                new List<IEventHandler> { eventHandler },
                (key, existing) =>
                {
                    existing.Add(eventHandler);
                    return existing;
                });

            _logger.LogDebug("Subscribed to event type: {EventType}", eventType.Name);

            return new EventSubscription(eventType, eventHandler, () => Unsubscribe(eventType, eventHandler));
        }

        /// <summary>
        /// Subscribe to events by name with dynamic handler
        /// </summary>
        public static EventSubscription Subscribe(string eventName, Func<object, CancellationToken, Task> handler)
        {
            var eventHandler = new DynamicEventHandler(handler);

            _namedHandlers.AddOrUpdate(eventName,
                new List<IEventHandler> { eventHandler },
                (key, existing) =>
                {
                    existing.Add(eventHandler);
                    return existing;
                });

            _logger.LogDebug("Subscribed to event name: {EventName}", eventName);

            return new EventSubscription(eventName, eventHandler, () => Unsubscribe(eventName, eventHandler));
        }

        /// <summary>
        /// Subscribe to multiple events with single handler
        /// </summary>
        public static MultiEventSubscription SubscribeToMultiple<THandler>(THandler handler)
            where THandler : class
        {
            var subscriptions = new List<EventSubscription>();
            var handlerType = typeof(THandler);
            var interfaces = handlerType.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>));

            foreach (var interfaceType in interfaces)
            {
                var eventType = interfaceType.GetGenericArguments()[0];
                var eventHandler = new ReflectionEventHandler(handler, eventType);

                _handlers.AddOrUpdate(eventType,
                    new List<IEventHandler> { eventHandler },
                    (key, existing) =>
                    {
                        existing.Add(eventHandler);
                        return existing;
                    });

                subscriptions.Add(new EventSubscription(eventType, eventHandler, 
                    () => Unsubscribe(eventType, eventHandler)));
            }

            return new MultiEventSubscription(subscriptions);
        }

        #endregion

        #region Event Filtering & Routing

        /// <summary>
        /// Subscribe with condition filter
        /// </summary>
        public static EventSubscription SubscribeIf<TEvent>(
            Func<TEvent, bool> condition,
            Func<TEvent, CancellationToken, Task> handler) where TEvent : class
        {
            return Subscribe<TEvent>(async (evt, ct) =>
            {
                if (condition(evt))
                {
                    await handler(evt, ct);
                }
            });
        }

        /// <summary>
        /// Subscribe with transformation
        /// </summary>
        public static EventSubscription SubscribeTransform<TEvent, TTransformed>(
            Func<TEvent, TTransformed> transformer,
            Func<TTransformed, CancellationToken, Task> handler) where TEvent : class
        {
            return Subscribe<TEvent>(async (evt, ct) =>
            {
                var transformed = transformer(evt);
                await handler(transformed, ct);
            });
        }

        /// <summary>
        /// Create event pipeline with multiple processing stages
        /// </summary>
        public static EventPipeline<TEvent> CreatePipeline<TEvent>() where TEvent : class
        {
            return new EventPipeline<TEvent>();
        }

        #endregion

        #region Event Aggregation

        /// <summary>
        /// Aggregate events over time window
        /// </summary>
        public static EventAggregator<TEvent> AggregateEvents<TEvent>(
            TimeSpan windowSize,
            Func<IEnumerable<TEvent>, Task> aggregateHandler) where TEvent : class
        {
            return new EventAggregator<TEvent>(windowSize, aggregateHandler);
        }

        /// <summary>
        /// Debounce events (only process latest in time window)
        /// </summary>
        public static EventSubscription DebounceEvents<TEvent>(
            TimeSpan debounceTime,
            Func<TEvent, CancellationToken, Task> handler) where TEvent : class
        {
            var debouncer = new EventDebouncer<TEvent>(debounceTime, handler);
            return Subscribe<TEvent>(debouncer.ProcessEvent);
        }

        #endregion

        #region Configuration

        /// <summary>
        /// Configure event bus options
        /// </summary>
        public static void Configure(Action<EventBusOptions> configure)
        {
            configure(_options);
        }

        /// <summary>
        /// Get event bus statistics
        /// </summary>
        public static EventBusStatistics GetStatistics()
        {
            return new EventBusStatistics
            {
                RegisteredEventTypes = _handlers.Keys.Count,
                RegisteredEventNames = _namedHandlers.Keys.Count,
                TotalHandlers = _handlers.Values.Sum(h => h.Count) + _namedHandlers.Values.Sum(h => h.Count),
                EventTypeHandlers = _handlers.ToDictionary(
                    kvp => kvp.Key.Name,
                    kvp => kvp.Value.Count
                ),
                EventNameHandlers = _namedHandlers.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Count
                )
            };
        }

        #endregion

        #region Private Methods

        private static async Task ExecuteHandlerSafely(IEventHandler handler, object eventData, 
            CancellationToken cancellationToken)
        {
            try
            {
                await handler.HandleAsync(eventData, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogException(ex, "Event handler failed: {HandlerType}", handler.GetType().Name);
                
                if (!_options.ContinueOnHandlerError)
                    throw;
            }
        }

        private static void Unsubscribe(Type eventType, IEventHandler handler)
        {
            if (_handlers.TryGetValue(eventType, out var handlers))
            {
                handlers.Remove(handler);
                if (handlers.Count == 0)
                {
                    _handlers.TryRemove(eventType, out _);
                }
            }
        }

        private static void Unsubscribe(string eventName, IEventHandler handler)
        {
            if (_namedHandlers.TryGetValue(eventName, out var handlers))
            {
                handlers.Remove(handler);
                if (handlers.Count == 0)
                {
                    _namedHandlers.TryRemove(eventName, out _);
                }
            }
        }

        #endregion
    }

    #region Event Handler Interfaces

    public interface IEventHandler
    {
        Task HandleAsync(object eventData, CancellationToken cancellationToken = default);
    }

    public interface IEventHandler<in TEvent> : IEventHandler where TEvent : class
    {
        Task HandleAsync(TEvent eventData, CancellationToken cancellationToken = default);
    }

    #endregion

    #region Event Handler Implementations

    internal class TypedEventHandler<TEvent> : IEventHandler where TEvent : class
    {
        private readonly Func<TEvent, CancellationToken, Task> _handler;

        public TypedEventHandler(Func<TEvent, CancellationToken, Task> handler)
        {
            _handler = handler;
        }

        public async Task HandleAsync(object eventData, CancellationToken cancellationToken = default)
        {
            if (eventData is TEvent typedEvent)
            {
                await _handler(typedEvent, cancellationToken);
            }
        }
    }

    internal class DynamicEventHandler : IEventHandler
    {
        private readonly Func<object, CancellationToken, Task> _handler;

        public DynamicEventHandler(Func<object, CancellationToken, Task> handler)
        {
            _handler = handler;
        }

        public async Task HandleAsync(object eventData, CancellationToken cancellationToken = default)
        {
            await _handler(eventData, cancellationToken);
        }
    }

    internal class ReflectionEventHandler : IEventHandler
    {
        private readonly object _handler;
        private readonly MethodInfo _handleMethod;

        public ReflectionEventHandler(object handler, Type eventType)
        {
            _handler = handler;
            _handleMethod = handler.GetType().GetMethod("HandleAsync", new[] { eventType, typeof(CancellationToken) })
                ?? throw new ArgumentException($"Handler does not implement HandleAsync for {eventType.Name}");
        }

        public async Task HandleAsync(object eventData, CancellationToken cancellationToken = default)
        {
            var result = _handleMethod.Invoke(_handler, new[] { eventData, cancellationToken });
            if (result is Task task)
            {
                await task;
            }
        }
    }

    #endregion

    #region Event Aggregation

    public class EventAggregator<TEvent> : IDisposable where TEvent : class
    {
        private readonly List<TEvent> _events = new();
        private readonly Timer _timer;
        private readonly Func<IEnumerable<TEvent>, Task> _aggregateHandler;
        private readonly object _lock = new();

        public EventAggregator(TimeSpan windowSize, Func<IEnumerable<TEvent>, Task> aggregateHandler)
        {
            _aggregateHandler = aggregateHandler;
            _timer = new Timer(ProcessAggregatedEvents, null, windowSize, windowSize);
            
            EventBusExtensions.Subscribe<TEvent>(AddEvent);
        }

        private void AddEvent(TEvent eventData)
        {
            lock (_lock)
            {
                _events.Add(eventData);
            }
        }

        private async void ProcessAggregatedEvents(object? state)
        {
            List<TEvent> eventsToProcess;
            
            lock (_lock)
            {
                if (_events.Count == 0)
                    return;
                    
                eventsToProcess = new List<TEvent>(_events);
                _events.Clear();
            }

            try
            {
                await _aggregateHandler(eventsToProcess);
            }
            catch (Exception ex)
            {
                var logger = PowNetLogger.GetLogger("EventAggregator");
                logger.LogException(ex, "Event aggregation failed");
            }
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }

    public class EventDebouncer<TEvent> where TEvent : class
    {
        private readonly TimeSpan _debounceTime;
        private readonly Func<TEvent, CancellationToken, Task> _handler;
        private Timer? _timer;
        private TEvent? _latestEvent;
        private readonly object _lock = new();

        public EventDebouncer(TimeSpan debounceTime, Func<TEvent, CancellationToken, Task> handler)
        {
            _debounceTime = debounceTime;
            _handler = handler;
        }

        public async Task ProcessEvent(TEvent eventData, CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                _latestEvent = eventData;
                _timer?.Dispose();
                _timer = new Timer(async _ =>
                {
                    try
                    {
                        if (_latestEvent != null)
                        {
                            await _handler(_latestEvent, cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        var logger = PowNetLogger.GetLogger("EventDebouncer");
                        logger.LogException(ex, "Debounced event handler failed");
                    }
                }, null, _debounceTime, Timeout.InfiniteTimeSpan);
            }

            await Task.CompletedTask;
        }
    }

    #endregion

    #region Event Pipeline

    public class EventPipeline<TEvent> where TEvent : class
    {
        private readonly List<Func<TEvent, CancellationToken, Task<TEvent>>> _stages = new();

        public EventPipeline<TEvent> AddStage(Func<TEvent, CancellationToken, Task<TEvent>> stage)
        {
            _stages.Add(stage);
            return this;
        }

        public EventPipeline<TEvent> AddStage(Func<TEvent, TEvent> stage)
        {
            _stages.Add(async (evt, ct) => stage(evt));
            return this;
        }

        public EventSubscription Subscribe()
        {
            return EventBusExtensions.Subscribe<TEvent>(ProcessThroughPipeline);
        }

        private async Task ProcessThroughPipeline(TEvent eventData, CancellationToken cancellationToken)
        {
            var current = eventData;
            
            foreach (var stage in _stages)
            {
                current = await stage(current, cancellationToken);
                if (current == null)
                    break;
            }
        }
    }

    #endregion

    #region Subscription Management

    public class EventSubscription : IDisposable
    {
        private readonly Action _unsubscribe;
        public Type? EventType { get; }
        public string? EventName { get; }
        public IEventHandler Handler { get; }

        public EventSubscription(Type eventType, IEventHandler handler, Action unsubscribe)
        {
            EventType = eventType;
            Handler = handler;
            _unsubscribe = unsubscribe;
        }

        public EventSubscription(string eventName, IEventHandler handler, Action unsubscribe)
        {
            EventName = eventName;
            Handler = handler;
            _unsubscribe = unsubscribe;
        }

        public void Dispose()
        {
            _unsubscribe();
        }
    }

    public class MultiEventSubscription : IDisposable
    {
        private readonly List<EventSubscription> _subscriptions;

        public MultiEventSubscription(List<EventSubscription> subscriptions)
        {
            _subscriptions = subscriptions;
        }

        public void Dispose()
        {
            foreach (var subscription in _subscriptions)
            {
                subscription.Dispose();
            }
        }
    }

    #endregion

    #region Configuration & Statistics

    public class EventBusOptions
    {
        public bool UseParallelExecution { get; set; } = true;
        public bool ContinueOnHandlerError { get; set; } = true;
        public int MaxConcurrentHandlers { get; set; } = Environment.ProcessorCount * 2;
    }

    public class EventBusStatistics
    {
        public int RegisteredEventTypes { get; set; }
        public int RegisteredEventNames { get; set; }
        public int TotalHandlers { get; set; }
        public Dictionary<string, int> EventTypeHandlers { get; set; } = new();
        public Dictionary<string, int> EventNameHandlers { get; set; } = new();
    }

    #endregion
}