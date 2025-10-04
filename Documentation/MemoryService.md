# MemoryService

High-level in-memory caching utility wrapping `IMemoryCache` with instrumentation, statistics, categorization, warming, and safe key enumeration. Designed to avoid fragile reflection of internal `MemoryCache` state by tracking keys explicitly.

---

## 1. Overview

Features:

- Central shared `IMemoryCache` instance (`SharedMemoryCache`) with size limit heuristics
- Key tracking without reflection
- Hit/miss/set/removal counters (global + per category = prefix before `::`)
- Expiration + eviction callback wiring for statistics
- Category derivation: `"Prefix::ActualKey"` ? category = `Prefix` else `General`
- Background cleanup (stale key pruning + optional GC pressure check)
- Cache warm-up helper for async sources

Thread Safety: All counters updated via `Interlocked`; dictionaries are `ConcurrentDictionary`; shared cache double-checked locking.

---

## 2. Key & Enumeration Helpers

### GetKeys(this IMemoryCache memoryCache) : ICollection

Returns a snapshot (array) of currently tracked keys.

```csharp
var keys = MemoryService.SharedMemoryCache.GetKeys();
foreach (var k in keys) Console.WriteLine(k);
```

### GetKeys<T>(this IMemoryCache memoryCache)

Typed filter over `GetKeys()`.

```csharp
var stringKeys = MemoryService.SharedMemoryCache.GetKeys<string>();
```

### GetKeysStartsWith(this IMemoryCache memoryCache, string startingWith) : List<string>

Efficient prefix search using span comparison.

```csharp
var authKeys = cache.GetKeysStartsWith("Auth::");
```

---

## 3. Mutating & Lookup Methods

### TryAdd(this IMemoryCache memoryCache, string key, object? val, TimeSpan? expiration = null)

Adds (after removing any prior entry) with optional absolute expiration. Always sets `Size = 1` to play nicely with size-limited caches. Registers eviction callback to update stats.

```csharp
cache.TryAdd("User::123", userDto, TimeSpan.FromMinutes(30));
```

Side Effects:

- Increments global set counter & category set counter
- Tracks key access time
- On eviction: increments removal counters and removes tracking entry

### GetWithStats<T>(this IMemoryCache memoryCache, string key) : T?

Fetches value and records hit/miss. Updates last-access timestamp on hit.

```csharp
var profile = cache.GetWithStats<UserProfile>("User::123");
if (profile == null) { /* load & TryAdd */ }
```

Return: Value or default(T) on miss / type mismatch.

### TryRemove(this IMemoryCache memoryCache, string key)

Removes an entry if present and records a removal.

```csharp
cache.TryRemove("User::123");
```

---

## 4. Shared Cache Lifecycle

### SharedMemoryCache (property)

Lazily creates a single `MemoryCache` with:

- `SizeLimit` (heuristic: quarter of observed memory up to 500MB, min 100MB baseline)
- `CompactionPercentage = 0.25`
- `ExpirationScanFrequency = 2 minutes`

```csharp
var cache = MemoryService.SharedMemoryCache; // instantiate if first access
```

Setter allows external replacement (disposing previous instance).

### DisposeSharedCache()

Disposes underlying shared cache, stops cleanup timer, clears statistics, and key tracking.

```csharp
MemoryService.DisposeSharedCache();
```

---

## 5. Metrics & Statistics

### GetCacheMetrics() : CacheMetrics

Aggregates global counters, per-category snapshots, active key count, and raw memory usage.

```csharp
var metrics = MemoryService.GetCacheMetrics();
Console.WriteLine(metrics);
```

Fields (CacheMetrics): `TotalHits`, `TotalMisses`, `TotalSets`, `TotalRemovals`, `HitRatio`, `CategoryStats`, `ActiveKeysCount`, `MemoryPressure`.

### GetCategoryStats(string category) : CacheStatistics?

Fetches mutable stats object for category (e.g., `User`).

```csharp
var userStats = MemoryService.GetCategoryStats("User");
```

### ClearStats()

Resets all global counters and clears category dictionary (does not wipe cache entries).

```csharp
MemoryService.ClearStats();
```

---

## 6. Warm-Up

### WarmUpCacheAsync(IEnumerable<KeyValuePair<string, Func<Task<object>>>> warmupData)

Executes each value factory, caching results for 1 hour (ignores individual failures).

```csharp
await MemoryService.WarmUpCacheAsync(new[]{
    new KeyValuePair<string,Func<Task<object>>>("Config::All", async () => await LoadConfigAsync()),
    new KeyValuePair<string,Func<Task<object>>>("User::Defaults", async () => await LoadDefaultsAsync())
});
```

---

## 7. Supporting Types

### CacheStatistics

Counters (hits, misses, sets, removals) with atomic increment methods and `GetSnapshot()` returning immutable `CacheStatisticsSnapshot { Hits, Misses, Sets, Removals, HitRatio }`.

### CacheMetrics (record)

Immutable aggregate for reporting. `ToString()` provides a concise summary.

### CacheStatisticsSnapshot (record)

Point-in-time immutable view of category stats.

---

## 8. Category Derivation

`GetCacheCategory(key)` splits on `"::"`. Example: `User::123` ? `User`; otherwise category = `General`. Use this prefix consistently to group metrics.

---

## 9. Cleanup & Memory Pressure

Background timer (5 min interval) removes keys not accessed in last 30 minutes (up to 100 per cycle). If total managed memory > 1GB triggers a Gen1 GC (`GC.Collect(1, Optimized)`).

---

## 10. Usage Pattern

```csharp
var cache = MemoryService.SharedMemoryCache;
string key = $"Product::{id}";
var item = cache.GetWithStats<ProductDto>(key);
if (item == null)
{
    item = await LoadProductAsync(id);
    cache.TryAdd(key, item, TimeSpan.FromMinutes(15));
}
var metrics = MemoryService.GetCacheMetrics();
Console.WriteLine($"HitRatio: {metrics.HitRatio:P2}");
```

---

## 11. Best Practices

- Always namespace keys with a category prefix for meaningful metrics.
- Avoid very large object graphs in cache; consider serialization or trimming.
- Use `WarmUpCacheAsync` during application startup for latency-sensitive lookups.
- Monitor `HitRatio`—a consistently low figure suggests poor key strategy or TTL mismatch.
- Apply distributed cache (e.g., Redis) for cross-instance scenarios; this service is in-memory local only.

---

## 12. Limitations

- No sliding expiration helper (can be added via `MemoryCacheEntryOptions.SlidingExpiration`).
- No eviction priority support (could extend options).
- Metrics are approximate under extreme contention (still atomic but may lag for category creation edge cases).
