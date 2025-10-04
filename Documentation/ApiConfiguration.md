# ApiConfiguration & Extensions

Defines per-endpoint (or logical API operation) configuration controlling access validation, caching and logging.

---
## Properties
| Property | Type | Meaning |
|----------|------|---------|
| ApiName | string | Logical name / identifier |
| CheckAccessLevel | CheckAccessLevel | Access rule evaluation mode |
| AllowedRoles / AllowedUsers | List<int>? | Explicit allow lists |
| DeniedRoles / DeniedUsers | List<int>? | Explicit deny lists (checked before allow) |
| CacheLevel | CacheLevel | None / PerUser / AllUsers |
| CacheSeconds | int | TTL for cache entry (seconds) |
| LogEnabled | bool | Enables logging of calls |

---
## Extension Methods

### IsCachingEnabled(this ApiConfiguration apiConf)
Returns true when cache level is `AllUsers` or `PerUser` AND `CacheSeconds > 0`.
```csharp
var cfg = new ApiConfiguration { CacheLevel = CacheLevel.AllUsers, CacheSeconds = 60 };
if (cfg.IsCachingEnabled())
{
    // Insert into cache
}
```

### IsLoggingEnabled(this ApiConfiguration apiConf)
Returns `LogEnabled` flag.
```csharp
if (cfg.IsLoggingEnabled()) logger.LogInformation("Caching active");
```

### GetCacheOptions(this ApiConfiguration apiConf)
Creates `MemoryCacheEntryOptions` with absolute expiration.
```csharp
var options = cfg.GetCacheOptions();
cache.Set(key, payload, options);
```

---
## Usage Pattern
```csharp
ApiConfiguration op = new()
{
    ApiName = "GetUser",
    CacheLevel = CacheLevel.PerUser,
    CacheSeconds = 120,
    LogEnabled = true
};

if (op.IsCachingEnabled())
{
    var cacheKey = $"user:{userId}";
    if (!cache.TryGetValue(cacheKey, out User dto))
    {
        dto = LoadUser(userId);
        cache.Set(cacheKey, dto, op.GetCacheOptions());
    }
    return dto;
}
```

---
## Notes
- Access rule lists (Allowed*/Denied*) are placeholders for higher-level authorization logic.
- `CheckAccessLevel` enum (not documented here) should define validation semantics.
