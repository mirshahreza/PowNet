# CacheExtensions

Advanced caching helpers providing single / multi-level caching, conditional caching, background refresh, warming, tagging, pattern invalidation, statistics and configurable provider registration.

> See also: [PerformanceExtensions](PerformanceExtensions.md) for memoization utilities and [PowNetConfiguration](PowNetConfiguration.md) for default cache settings.

---
## Key Concepts
| Concept | Description |
|---------|-------------|
| Providers | Pluggable `ICacheProvider` instances (default in?memory) registered by name. |
| Metadata Presence | Presence determined via `GetWithMetadataAsync` (supports value types reliably). |
| Multi-Level | L1 (fast / memory) + optional L2 (e.g., distributed) fetch chain. |
| Tags | Keys can carry tag sets enabling group invalidation. |
| Pattern Invalidation | Regex–like key removal (supports `*` wildcard via conversion). |
| Background Refresh | Returns stale value while refreshing if refresh threshold reached. |
| Statistics | Hit / miss counts per provider & overall. |
| Thread Safety | All public operations are thread-safe; background refresh may trigger multiple overlapping refresh tasks (see Mitigations). |

---
## Core APIs
| API | Purpose |
|-----|---------|
| `CacheAsync(factory, key?, expiration?, provider?, options?)` | Cache async factory result (auto key fallback). |
| `Cache(factory, key?, expiration?, provider?, options?)` | Sync variant. |
| `MultiLevelCacheAsync(factory, key, l1Exp?, l2Exp?, l1Provider?, l2Provider?)` | Two-tier caching strategy. |
| `CacheIfAsync(factory, predicate, key, expiration?, provider?)` | Cache only if predicate passes. |
| `RefreshBehindAsync(factory, key, expiration, refreshThreshold, provider?)` | Lazy refresh in background when threshold reached. |
| `WarmCacheAsync(key, factory, expiration, provider?)` | Pre-populate one entry. |
| `WarmCacheBatchAsync(dict, expiration, provider?, maxConcurrency?)` | Batch warm multiple entries concurrently. |
| `InvalidateByTagsAsync(tags...)` | Evict entries containing any provided tag. |
| `InvalidateByPatternAsync(pattern, provider?)` | Regex/wildcard based eviction. |
| `GetStatistics()` / `ResetStatistics()` | Aggregate hit / miss metrics. |
| `RegisterCacheProvider(name, provider)` / `GetCacheProvider(name?)` | Provider management. |

---
## CacheOptions
| Property | Effect | Default |
|----------|--------|---------|
| CacheNullValues | Store null results | false |
| CacheEmptyStrings | Store empty strings | false |
| CacheEmptyCollections | Store empty collections | false |
| Tags | Attach tags to entry | [] |

### Example with Tags
```csharp
var data = await (() => LoadDashboardAsync())
    .CacheAsync("Dashboard::Main", TimeSpan.FromMinutes(5), options: new CacheOptions{ Tags = new[]{"dashboard","v1"} });
// Invalidate all dashboard entries later:
await CacheExtensions.InvalidateByTagsAsync("dashboard");
```

---
## Example – Basic Usage
```csharp
Func<Task<User>> loadUser = () => repo.GetUserAsync(id);
var user = await loadUser.CacheAsync($"User::{id}", TimeSpan.FromMinutes(10));
```

## Example – Conditional
```csharp
var summary = await (() => LoadReportAsync())
    .CacheIfAsync(r => r.Items.Any(), "Report::Daily", TimeSpan.FromMinutes(15));
```

## Example – Background Refresh
```csharp
var value = await (() => FetchMetricsAsync())
    .RefreshBehindAsync("Metrics::Latest", TimeSpan.FromMinutes(5), refreshThreshold: TimeSpan.FromMinutes(4));
```

## Example – Multi-Level
```csharp
var dto = await (() => FetchProductAsync(id))
    .MultiLevelCacheAsync($"Product::{id}", TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(30), l1Provider:"memory", l2Provider:"distributed");
```

## Example – Warming
```csharp
await CacheExtensions.WarmCacheBatchAsync(new Dictionary<string,Func<Task<int>>>{
    ["Cfg::A"] = () => LoadA(),
    ["Cfg::B"] = () => LoadB()
}, TimeSpan.FromMinutes(10));
```

---
## Invalidation
```csharp
await CacheExtensions.InvalidateByTagsAsync("User","Profile");
await CacheExtensions.InvalidateByPatternAsync("User::.*");
```

---
## Statistics
```csharp
var stats = CacheExtensions.GetStatistics();
Console.WriteLine($"Hits={stats.TotalHits} Misses={stats.TotalMisses} HitRate={stats.HitRate:F2}%");
```

---
## Provider Implementation (MemoryCacheProvider)
- JSON serialize values for storage.
- Metadata: CreatedAt, ExpiresAt, LastAccessed, Tags.
- Cleanup timer (5 min period) removes expired entries.
- Pattern invalidation via Regex (supports `*` wildcard -> `.*`).

---
## Background Refresh Semantics
- If entry exists and `CreatedAt + refreshThreshold < UtcNow`, a background task executes factory and updates value.
- Caller receives current (possibly stale) value immediately.
- Errors during background refresh are logged; value remains.

**Mitigation for duplicate refresh:** If high contention, wrap factory with an internal semaphore to serialize refresh or add a distributed lock in a custom provider.

---
## Multi-Level Flow
1. Check L1 (fast).
2. If miss, check L2.
3. If L2 hit, promote to L1.
4. If both miss, execute factory and populate both.

---
## Best Practices
- Use key namespaces (`Domain::Entity::Id`).
- Choose conservative defaults for expiration then tune.
- Avoid caching extremely large objects unless memory pressure monitored.
- Prefer conditional caching to avoid storing empty or meaningless responses.
- Apply tags for grouped invalidation (e.g., per-tenant, per-feature).

---
## Limitations
- Provided distributed layer is conceptual; implement a real `ICacheProvider` (Redis, etc.) for multi-instance scenarios.
- No per-entry size tracking or eviction policy beyond TTL.
- Background refresh does not deduplicate concurrent refresh triggers (could add lock/coalescing if needed).

---
## Troubleshooting
| Symptom | Possible Cause | Action |
|---------|----------------|--------|
| Value not cached | Predicate failed / options disallow empty / null | Inspect `CacheOptions` & predicate logic |
| Frequent factory calls | Expiration too low / no metadata hit / different keys | Verify key generation & expiration |
| Tag invalidation misses entries | Tag not attached or case mismatch | Ensure `CacheOptions.Tags` populated consistently |
| Pattern invalidation skips keys | Pattern not matching or wildcard misuse | Test regex at runtime / escape special chars |
| Stale values linger | Refresh threshold too high | Lower threshold or force invalidate |

---
## Recent Enhancements
- Added: Tag & pattern invalidation, warming batch, background refresh threshold, statistics API using metadata presence.
- Added: Multi-level promotion logic and conditional caching guards.
- Added: Troubleshooting, Thread Safety, cross links & tag example.
