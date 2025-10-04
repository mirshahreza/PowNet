# ApiCallService & ApiCallInfo

Captures per-request routing metadata (controller, action, logical namespace) and supplies helpers for configuration lookup and cache key generation.

---
## 1. ApiCallInfo (Record)
| Property | Meaning |
|----------|---------|
| RequestPath | Raw path (e.g. `/api/v1/users/list`) |
| NamespaceName | Logical grouping inferred from the path (heuristic) |
| ControllerName | Controller route value (e.g. `Users`) |
| ApiName | Action route value (e.g. `List`) |

Creation normally occurs via the `HttpContext.GetApiCallInfo()` extension but instances can be created manually for tests or background scenarios.

```csharp
var info = new ApiCallInfo("/api/v1/users/list", "api/v1/users", "Users", "List");
```

> Namespace derivation is heuristic string manipulation; tailor it if you use custom route patterns or versioning formats.

---
## 2. Extension Methods
| Method | Purpose |
|--------|---------|
| GetApiCallInfo(this HttpContext) | Build an `ApiCallInfo` from current route data |
| GetConfig(this ApiCallInfo) | Retrieve `ControllerConfiguration` for the resolved controller |
| GetCacheKey(this ApiCallInfo, ApiConfiguration, UserServerObject) | Deterministic response cache key respecting cache level |

(An obsolete alias `GetAppEndWebApiInfo` still exists but will be removed in a future release.)

---
## 3. Cache Key Strategy
Pattern: `Response::{ControllerName}_{ApiName}`
- If `CacheLevel == PerUser` ? append `_UserName`
- For multi-version APIs consider prefixing version: `v1::Response::Users_List`

```csharp
var apiInfo = context.GetApiCallInfo();
var controllerCfg = apiInfo.GetConfig();
var apiCfg = controllerCfg.ApiConfigurations.FirstOrDefault(a => a.ApiName == apiInfo.ApiName)
             ?? new ApiConfiguration { ApiName = apiInfo.ApiName };

if (apiCfg.IsCachingEnabled())
{
    var cacheKey = apiInfo.GetCacheKey(apiCfg, currentUser);
    if (memoryCache.TryGetValue(cacheKey, out object cached))
        return cached;

    var result = await ExecuteAsync();
    memoryCache.Set(cacheKey, result, apiCfg.GetCacheOptions());
    return result;
}
return await ExecuteAsync();
```

---
## 4. Configuration Lookup Flow
1. Build `ApiCallInfo` from `HttpContext`.
2. Resolve `ControllerConfiguration` via `apiInfo.GetConfig()`.
3. Find matching `ApiConfiguration` (fallback to default if missing).
4. Apply behavior (caching, logging, access rules) based on the configuration.

---
## 5. Error Handling Recommendations
- Wrap `GetApiCallInfo()` in try/catch inside custom middleware if malformed routes must not surface as 500 errors.
- Provide a safe fallback `ApiConfiguration` when no entry exists to avoid null checks.
- Log namespace/controller/action triplets when a configuration is missing to aid auditing.

---
## 6. Performance Notes
- Route value extraction is O(1); string replacements for namespace derivation are minor. Avoid re-computing `ApiCallInfo` multiple times—cache in `HttpContext.Items` if accessed repeatedly.
- Cache key generation performs simple concatenation; pre-allocate only for extreme hot paths.

---
## 7. Customization Ideas
| Need | Approach |
|------|---------|
| API Versioning | Inject version into `NamespaceName` or extend `ApiCallInfo` with `Version` field |
| Content Negotiation Variant | Add media type or format segment into cache key |
| Multi-Tenant | Append tenant id before (or instead of) username for `PerUser` scope |
| Access Policies | Map `(ControllerName, ApiName)` to policy names evaluated by an authorization service |

---
## 8. Limitations
- Namespace derivation may mis-handle repeated substrings.
- Assumes conventional `controller` and `action` route tokens are present; exotic routing may require a custom extractor.

---
## 9. Testing Tips
```csharp
var info = new ApiCallInfo("/api/orders/get", "api/orders", "Orders", "Get");
var key = info.GetCacheKey(new ApiConfiguration { ApiName = "Get", CacheLevel = CacheLevel.AllUsers, CacheSeconds = 60 }, testUser);
Assert.Equal("Response::Orders_Get", key);
```

---
*Document updated; obsolete method name retained only as compatibility alias.*
