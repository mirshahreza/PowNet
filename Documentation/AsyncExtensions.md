# AsyncExtensions

Helpers for common Task / async patterns: timeouts, retries, fire-and-forget with safety, sequential & parallel composition utilities.

---
## Overview
Capabilities:
- Apply cancellation / timeout wrappers
- Retry transient operations (exponential backoff)
- Safely launch background tasks capturing exceptions
- Convert sync delegates to Task and vice versa

---
## Representative Methods
| Method | Purpose |
|--------|---------|
| WithTimeout<T>(this Task<T> task, TimeSpan timeout) | Cancel / fail if not completed in time |
| RetryAsync<T>(Func<Task<T>> factory, int maxRetries, TimeSpan? delay, Func<Exception,bool>? shouldRetry) | Retry transient failures |
| FireAndForgetSafe(this Task task, Action<Exception>? handler = null) | Launch & observe exceptions |
| WhenAllSafe(this IEnumerable<Task> tasks) | Await all, aggregating errors |
| Sequence<T>(this IEnumerable<Func<Task<T>>> tasks) | Execute sequentially collecting results |

(Align with actual implementation.)

---
## Usage Example
```csharp
var result = await FetchRemoteAsync()
    .WithTimeout(TimeSpan.FromSeconds(2));

var value = await (() => FetchRemoteAsync())
    .RetryAsync(maxRetries:3, delay:TimeSpan.FromMilliseconds(200));

backgroundWork.FireAndForgetSafe(ex => logger.LogError(ex, "Background failed"));
```

---
## Notes
- Prefer cancellation tokens over hard timeouts when upstream can signal cancel.
- Always provide logging handler for FireAndForget patterns.

---
## Limitations
- Backoff jitter may be absent; add for large-scale distributed retries.
- `FireAndForgetSafe` still runs on thread pool; heavy CPU tasks should be queued explicitly.
