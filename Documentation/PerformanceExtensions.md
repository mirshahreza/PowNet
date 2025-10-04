# PerformanceExtensions

High-level performance facilitation helpers: memoization, batch & pipeline processing, lazy initialization, memory & GC utilities, retry with backoff, operational monitoring, and lightweight processing pipelines.

---
## 1. Caching & Memoization
### Memoize<TResult>(this Func<TResult> func)
Single-result lazy (thread-safe via `Lazy<T>`). Useful when a pure, expensive computation is required once.
```csharp
Func<int> expensive = () => LoadConfig();
var cached = expensive.Memoize();
int a = cached(); // computes
int b = cached(); // cached value
```

### Memoize<TParam,TResult>(this Func<TParam,TResult> func) where TParam:notnull
Caches results per distinct parameter using `ConcurrentDictionary`.
```csharp
Func<string,int> parser = s => int.Parse(s);
var memo = parser.Memoize();
int x = memo("42");
```

### MemoizeWithExpiration<TResult>(this Func<TResult> func, TimeSpan expiration)
Time?boxed value cache keyed as "default"; refreshes after expiry.
```csharp
var cachedNow = (() => DateTime.UtcNow).MemoizeWithExpiration(TimeSpan.FromSeconds(5));
```

### MemoizeAsync<TResult>(this Func<Task<TResult>> func)
Lazily invokes async factory once; subsequent awaits share same task.
```csharp
Func<Task<string>> loader = async () => await FetchAsync();
var memoAsync = loader.MemoizeAsync();
string v = await memoAsync();
```

---
## 2. Batch Processing
### ProcessInBatchesAsync<T>(this IEnumerable<T> source, Func<IEnumerable<T>,Task> processor, int batchSize=100, int maxConcurrency=4, CancellationToken ct=default)
Splits sequence into `batchSize` chunks; processes each concurrently up to `maxConcurrency` (semaphore throttled).
```csharp
await items.ProcessInBatchesAsync(async batch => await SaveRange(batch), batchSize:500, maxConcurrency:3);
```

### TransformInBatchesAsync<TSource,TResult>(...)
Maps items with batched concurrency and collects results into `ConcurrentBag<TResult>`.
```csharp
var outputs = await ids.TransformInBatchesAsync(async id => await LoadAsync(id));
```

---
## 3. Lazy Loading
### CreateLazy<T>(Func<T> factory, bool isThreadSafe = true)
Creates a configured `Lazy<T>` (execution + publication or none).
```csharp
var lazyCtx = PerformanceExtensions.CreateLazy(() => BuildContext());
```

### CreateAsyncLazy<T>(Func<Task<T>> factory)
Wraps async factory returning `AsyncLazy<T>` (awaitable wrapper).
```csharp
var lazyUser = PerformanceExtensions.CreateAsyncLazy(() => LoadUserAsync());
var user = await lazyUser;
```

---
## 4. Pipeline Processing
### CreatePipeline<T>(this IEnumerable<T> source)
Begins fluent processing pipeline.
```csharp
var pipeline = data.CreatePipeline()
    .AddStage(x => x * 2)
    .AddStage(x => x + 1);
var resultArray = pipeline.ToArray();
```

### AddStage / AddAsyncStage
Adds synchronous or asynchronous transformation stages.
```csharp
var pipe = data.CreatePipeline()
               .AddStage(x => x.Trim())
               .AddAsyncStage(async s => await NormalizeAsync(s));
```

Supporting methods on `ProcessingPipeline<T>`:
- `Transform` / `TransformAsync`
- `Filter`, `Take`, `Skip`
- `ToListAsync()`, `ToArray()`, `AsEnumerable()```

---
## 5. Memory Optimization
### ProcessLarge<TSource,TResult>(this IEnumerable<TSource> src, Func<TSource,TResult> processor, int bufferSize=1000)
Processes large sources in internal buffers to reduce peak memory.
```csharp
var projected = largeSeq.ProcessLarge(f => Map(f), bufferSize:2000).ToList();
```

### ForceGarbageCollection()
Forces full GC and returns bytes freed (diagnostic use only).
```csharp
long freed = PerformanceExtensions.ForceGarbageCollection();
```

---
## 6. Retry Utilities
### RetryAsync<T>(this Func<Task<T>> operation, int maxRetries=3, TimeSpan? initialDelay=null, double backoffMultiplier=2.0, Func<Exception,bool>? shouldRetry=null)
Executes with exponential backoff; logs warning on retry; stops retrying if `shouldRetry` returns false.
```csharp
int value = await (() => FetchRemoteInt())
    .RetryAsync(maxRetries:5, initialDelay:TimeSpan.FromMilliseconds(200), shouldRetry: ex => ex is TimeoutException);
```

### Retry<T>(this Func<T> operation, ...)
Synchronous counterpart (uses `Thread.Sleep`).
```csharp
var data = (() => ReadLocalFile()).Retry(maxRetries:4);
```

---
## 7. Monitoring & Diagnostics
### MonitorPerformance<T>(this Func<T> func, string methodName="", string filePath="", int lineNumber=0, bool logSlowOperations=true, TimeSpan? slowThreshold=null)
Captures timing with `DiagnosticsManager.MeasurePerformance` + optional slow call logging.
```csharp
var result = (() => Compute()).MonitorPerformance(slowThreshold:TimeSpan.FromMilliseconds(250));
```

### MonitorPerformanceAsync<T>(this Func<Task<T>> func, ...)
Async variant with default threshold 2000 ms.
```csharp
var dto = await (() => LoadDtoAsync()).MonitorPerformanceAsync(slowThreshold:TimeSpan.FromMilliseconds(500));
```

---
## 8. Supporting Classes
### TimedCache<T>
Monotonic-clock (Stopwatch) based expiring cache: `GetOrAdd(key, factory, expiration)` with periodic cleanup.

### CacheItem<T>
Stores `Value` + `MonotonicExpiresAtTick` (Stopwatch ticks).

### AsyncLazy<T>
Awaitable lazy wrapper exposing `Value` Task and custom `GetAwaiter()`.
```csharp
var lazyCfg = new AsyncLazy<Config>(() => LoadConfigAsync());
var cfg = await lazyCfg;
```

### ProcessingPipeline<T>
Immutable chain of transformations supporting sync & sequential async steps; composition returns new pipeline instances.

Key members:
- `Transform`, `TransformAsync`, `Filter`, `Take`, `Skip`
- `ToListAsync()`, `ToArray()`, `AsEnumerable()`

Limitations: async transform path currently blocks (calls `.GetAwaiter().GetResult()`) when integrating; for high-latency operations adapt pattern or redesign to full async pipeline.

---
## Usage Scenario
```csharp
// Combine: batching + retry + monitoring
await ids.ProcessInBatchesAsync(async batch =>
{
    await (() => PersistAsync(batch)).RetryAsync(maxRetries:3);
}, batchSize:250, maxConcurrency:4);

var pipeline = records.CreatePipeline()
    .AddStage(r => Normalize(r))
    .AddStage(r => Enrich(r));
var arr = pipeline.ToArray();
```

---
## Recommendations & Notes
- Keep `batchSize` tuned: too large ? memory pressure; too small ? overhead.
- Memoization for functions with side effects may cause stale data; ensure purity.
- Exponential backoff should include jitter in high contention scenarios (not implemented).
- Forced GC should not be used in production request path.

---
*Documentation manually authored.*
