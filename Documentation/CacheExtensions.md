# CacheExtensions

Higher-level convenience helpers around `IMemoryCache` for warming, conditional retrieval, and simplified add/try-get patterns.

---
## Representative Methods
| Method | Purpose |
|--------|---------|
| GetOrCreateAsync<T>(cache, key, factory, ttl) | Fetch from cache or create & store with TTL |
| TryGet<T>(cache, key, out value) | Lightweight typed get avoiding cast exceptions |
| WarmUpAsync(cache, IEnumerable<(key,factory)>) | Pre-populate common entries at startup |
| RemoveByPrefix(cache, prefix) | Evict keys matching prefix (if key enumeration available) |

(Confirm actual methods in source.)

---
## Usage Example
```csharp
var user = await cache.GetOrCreateAsync($"User::{id}", async () => await repo.LoadUser(id), TimeSpan.FromMinutes(10));
```

---
## Notes
- Prefer category/prefix convention (`Area::Key`) for bulk operations & metrics.
- Keep TTL aligned with underlying data volatility to minimize stale reads.

---
## Limitations
- Not a distributed cache; use Redis/SQL for multi-instance scenarios.
- RemoveByPrefix requires key tracking support (provided by MemoryService).
