# EventBusExtensions

Extension helpers for publishing and subscribing to an in-process (or pluggable) event bus abstraction.

---
## Representative Methods
| Method | Purpose |
|--------|---------|
| PublishAsync<TEvent>(this IEventBus bus, TEvent evt) | Send event to all subscribers |
| Subscribe<TEvent>(this IEventBus bus, Func<TEvent,Task> handler) | Register async handler |
| Unsubscribe<TEvent>(this IEventBus bus, Func<TEvent,Task> handler) | Remove handler |
| PublishSync<TEvent>(this IEventBus bus, TEvent evt) | Synchronous dispatch variant |

(Adjust according to actual interface names.)

---
## Example
```csharp
bus.Subscribe<UserCreatedEvent>(async e => await indexer.IndexUser(e.UserId));
await bus.PublishAsync(new UserCreatedEvent{ UserId = 5 });
```

---
## Guidance
- Keep event handlers idempotent; multiple deliveries may occur in distributed variants.
- Avoid heavy blocking work inside synchronous dispatch.

---
## Extension Ideas
| Need | Idea |
|------|------|
| Dead-letter handling | Capture failed events for later replay |
| Tracing | Correlate events with request/activity IDs |
