# EventBusExtensions

Rich in-process (pluggable) event bus helpers providing publish / subscribe, retry, scheduling, filtering, transformation, aggregation, debouncing, pipelines and basic statistics.

---
## Key Features (Implemented)
| Category | API | Notes |
|----------|-----|-------|
| Publish | `PublishAsync<TEvent>(this TEvent evt)` | Dispatch to all typed + named handlers (sequential or parallel) |
| Retry Publish | `PublishWithRetryAsync<TEvent>(..., maxRetries, delay)` | Exponential backoff (delay *2 each attempt) – stops on first success |
| Schedule | `ScheduleEvent<TEvent>(this TEvent evt, DateTime when)` | Fire later (uses `Task.Delay`); immediate if past time |
| Subscribe (typed) | `Subscribe<TEvent>(Func<TEvent,CancellationToken,Task>)` / `Subscribe<TEvent>(Action<TEvent>)` | Returns `EventSubscription` (IDisposable) |
| Subscribe (named) | `Subscribe(string eventName, Func<object,CancellationToken,Task>)` | Dynamic scenarios / duck typing |
| Multi?interface Handler | `SubscribeToMultiple<THandler>(THandler handler)` | Scans interfaces implementing `IEventHandler<T>` and registers all |
| Conditional | `SubscribeIf<TEvent>(predicate, handler)` | Executes handler only when predicate true |
| Transform | `SubscribeTransform<TEvent,TOut>(transformer, handler)` | Lightweight mapping before handler |
| Pipeline | `CreatePipeline<TEvent>().AddStage(...).Subscribe()` | Ordered async / sync stages, stops if a stage returns null |
| Aggregation | `AggregateEvents<TEvent>(window, aggregateHandler)` | Buffers events and flushes every window |
| Debounce | `DebounceEvents<TEvent>(time, handler)` | Processes only latest event in a time window |
| Statistics | `GetStatistics()` | Counts of registered types / names / handlers |
| Options | `Configure(o => { ... })` | `UseParallelExecution`, `ContinueOnHandlerError`, `MaxConcurrentHandlers` |

---
## Usage Examples
### Basic Publish & Subscribe
```csharp
var sub = EventBusExtensions.Subscribe<UserCreated>(e => Console.WriteLine($"User {e.Id}"));
await new UserCreated{ Id = 10 }.PublishAsync();
sub.Dispose();
```

### Retry Publish
```csharp
await new SyncNeeded { Id = 1 }
    .PublishWithRetryAsync(maxRetries:3, delay: TimeSpan.FromMilliseconds(100));
```

### Scheduling
```csharp
new NightlyJob { Date = DateTime.UtcNow }.ScheduleEvent(DateTime.UtcNow.AddMinutes(5));
```

### Conditional Subscription
```csharp
EventBusExtensions.SubscribeIf<OrderPlaced>(o => o.Total > 1000, async (o, ct) => await AlertAsync(o));
```

### Transforming
```csharp
EventBusExtensions.SubscribeTransform<OrderPlaced,HighValueOrder>(
    o => new HighValueOrder(o.Id, o.Total),
    async (ho, ct) => await AuditAsync(ho));
```

### Pipeline
```csharp
EventBusExtensions.CreatePipeline<InvoiceGenerated>()
    .AddStage(inv => { inv.Normalize(); return inv; })
    .AddStage(async (inv, ct) => { await PersistAsync(inv); return inv; })
    .Subscribe();
```

### Aggregation
```csharp
var agg = EventBusExtensions.AggregateEvents<MetricSample>(TimeSpan.FromSeconds(10), async batch =>
{
    var avg = batch.Average(x => x.Value);
    await StoreAverageAsync(avg);
});
// dispose agg when done
```

### Debounce
```csharp
EventBusExtensions.DebounceEvents<SearchQuery>(TimeSpan.FromMilliseconds(300), async (q, ct) =>
{
    await ExecuteSearchAsync(q.Text);
});
```

---
## Options
```csharp
EventBusExtensions.Configure(o =>
{
    o.UseParallelExecution = true;            // default true
    o.ContinueOnHandlerError = false;         // bubble first handler error
    o.MaxConcurrentHandlers = Environment.ProcessorCount * 2; // reserved for future throttling
});
```
- When `ContinueOnHandlerError` = true: handler exceptions are logged and suppressed.
- When false: first failing handler aborts the publish (later handlers may or may not have started depending on execution mode).

---
## Error Handling & Retries
`PublishWithRetryAsync` only retries when an exception bubbles. If errors are suppressed (ContinueOnHandlerError = true) the publish is considered successful (no retry).

---
## Scheduling Notes
- Uses `Task.Delay`; not persistent – missed after process restart.
- For durable scheduling integrate an external job system.

---
## Aggregation vs Debounce
| Feature | Aggregation | Debounce |
|---------|-------------|----------|
| Purpose | Collect many -> batch handle | Reduce noisy bursts to last item |
| State   | Accumulates list | Stores single latest |
| Trigger | Timer interval | Timer after last event |

---
## Pipelines
Stages can be sync (`Func<T,T>`) or async (`Func<T,CancellationToken,Task<T>>`). Returning `null` (for reference types) stops further processing.

---
## Statistics Example
```csharp
var stats = EventBusExtensions.GetStatistics();
Console.WriteLine($"Handlers: {stats.TotalHandlers}");
```

---
## Best Practices
- Keep handlers idempotent and side?effect aware.
- Avoid long blocking operations – prefer async.
- For cross?process distribution, wrap these APIs with a transport (e.g., message broker) and translate events.
- Use conditional / transform subscriptions to keep handlers cohesive.

---
## Limitations
- In?memory only; no built?in persistence, ordering guarantee or at?least?once semantics.
- No automatic dead?letter queue (add in outer layer if required).

---
## Change Log (Recent Enhancements)
- Added: Retry publishing with exponential backoff.
- Added: Scheduled events, debouncing, aggregation & pipelines.
- Added: Conditional & transform subscriptions.
- Added: Statistics snapshot API & configurable error continuation.
