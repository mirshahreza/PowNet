# PerformanceExtensions

Comprehensive utilities for memoization, batching, pipelines, lazy initialization, retries, memory/GC diagnostics and performance monitoring.

> See also: [CacheExtensions](CacheExtensions.md) for key/value caching & background refresh, and [DiagnosticsExtensions](DiagnosticsExtensions.md) for low-level timing helpers.

---
## Feature Matrix
| Area | APIs | Notes |
|------|------|------|
| Memoization | `Memoize`, `Memoize<T,TRes>`, `MemoizeAsync`, `MemoizeWithExpiration` | Expiring cache uses stopwatch ticks; sub?20ms windows may elapse early. |
| Batch Processing | `ProcessInBatchesAsync`, `TransformInBatchesAsync` | Concurrency limited by `maxConcurrency` (semaphore). |
| Lazy | `CreateLazy`, `CreateAsyncLazy`, `AsyncLazy<T>` | Async wrapper defers execution until first await. |
| Pipeline | `CreatePipeline` + `AddStage` / `AddAsyncStage` | Sequential pipeline; async stage composition currently not parallelized. |
| Large Sequence | `ProcessLarge` | Windowed projection to lower allocation spikes. |
| Retry | `RetryAsync`, `Retry` | Exponential backoff (delay *= multiplier). No jitter. |
| Monitoring | `MonitorPerformance`, `MonitorPerformanceAsync` | Captures timings, optional slow threshold logging. |
| Enumeration Metrics | `MeasureEnumeration` | Returns (array, duration, memoryDelta) – memory delta may be negative. |
| GC / Memory | `ForceGarbageCollection()` | Full GC, returns approx bytes freed (observational). |

---
## Memoization Comparison
| Variant | Keying | Expiration | Thread Safety | Use Case |
|---------|-------|------------|---------------|----------|
| `Memoize` | Single (no args) | No | Via `Lazy<T>` | Expensive init once |
| `Memoize<T,TRes>` | Argument value | No | ConcurrentDictionary | Pure deterministic function |
| `MemoizeAsync` | Single | No | Task sharing | Async expensive load once |
| `MemoizeWithExpiration` | Single | Yes (absolute) | Concurrent read/refresh after expiry | Time-bound expensive value |

---
## Updated Guidance
- Use memoization only for pure or effectively idempotent operations.
- Favor batch APIs (`ProcessInBatchesAsync`) when calling external services in bursts.
- For retries under heavy contention, add randomized jitter: `delay += TimeSpan.FromMilliseconds(Random.Shared.Next(25,75));`.
- Treat negative memory deltas as zero in reporting metrics.

---
## Composed Examples
### Retry + Monitoring
```csharp
var result = await (() => FetchRemoteAsync())
    .RetryAsync(maxRetries:4, initialDelay:TimeSpan.FromMilliseconds(150))
    .MonitorPerformanceAsync(methodName:"FetchRemoteAsync", slowThreshold:TimeSpan.FromMilliseconds(500));
```

### Batch + Pipeline
```csharp
await source.ProcessInBatchesAsync(async batch =>
{
    var cleaned = batch.CreatePipeline()
        .AddStage(x => Normalize(x))
        .AddStage(x => Enrich(x))
        .ToArray();
    await PersistAsync(cleaned);
}, batchSize:500, maxConcurrency:3);
```

### Async Lazy + Expiring Memoization
```csharp
var settingsLazy = PerformanceExtensions.CreateAsyncLazy(LoadSettingsAsync);
var tokenMemo = ((Func<string>)(() => AcquireToken())).MemoizeWithExpiration(TimeSpan.FromMinutes(10));
var (settings, token) = (await settingsLazy.Value, tokenMemo());
```

---
## Handling Negative Memory Delta
When GC reclaims previously allocated objects during measurement you may see a negative `memoryDelta`:
```csharp
var (items, elapsed, mem) = largeQuery.MeasureEnumeration();
var effectiveMem = Math.Max(0, mem);
```

---
## Limitations
- No internal coalescing for simultaneous expired memo refreshes.
- Pipelines execute on caller thread; long-running async operations may benefit from a redesign to streaming TPL Dataflow / Channels.
- Retry lacks cancellation token over per-attempt delay (wrap externally for advanced control).

---
## Recent Notes
- Expanded documentation with comparison table & composed usage patterns.
- Clarified memory delta semantics and negative value handling.

*Documentation manually authored.*
