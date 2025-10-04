# ApiCallService & ApiCallInfo

Utilities for capturing metadata about an incoming Web API request (controller, action, logical namespace) and supporting cache key generation and configuration lookup.

---
## 1. ApiCallInfo (Record)
| Property | Meaning |
|----------|---------|
| RequestPath | Raw request path (e.g. `/api/v1/users/list`) |
| NamespaceName | Logical grouping inferred from path (heuristic) |
| ControllerName | Controller route value |
| ApiName | Action (method) route value |

Construction is normally performed by `HttpContext.GetAppEndWebApiInfo()`; you can also instantiate manually.

```csharp
var info = new ApiCallInfo("/api/v1/users/list", "api/v1/users", "Users", "List");
```

> Current namespace derivation is string replacement based and may need refinement for complex routing.

---
## 2. Extension Methods
### GetAppEndWebApiInfo(this HttpContext context)
Extracts routing values and builds an `ApiCallInfo`. Throws if route values are missing.

```csharp
var apiInfo = context.GetAppEndWebApiInfo();
```

### GetCacheKey(this ApiCallInfo apiInfo, ApiConfiguration apiConf, UserServerObject user)
Deterministic cache key: `Response::{Controller}_{ApiName}` with optional `_UserName` suffix when `CacheLevel.PerUser`.

```csharp
string key = apiInfo.GetCacheKey(apiCfg, currentUser);
```

### GetConfig(this ApiCallInfo apiInfo)
Loads `ControllerConfiguration` for the namespace + controller.

```csharp
var controllerConfig = apiInfo.GetConfig();
var endpointCfg = controllerConfig.ApiConfigurations.FirstOrDefault(a => a.ApiName == apiInfo.ApiName);
```

---
## 3. Typical Caching Flow
```csharp
var info = context.GetAppEndWebApiInfo();
var ctrlCfg = info.GetConfig();
var apiCfg = ctrlCfg.ApiConfigurations.FirstOrDefault(a => a.ApiName == info.ApiName)
             ?? new ApiConfiguration { ApiName = info.ApiName };

if (apiCfg.IsCachingEnabled())
{
    string cacheKey = info.GetCacheKey(apiCfg, currentUser);
    if (memoryCache.TryGetValue(cacheKey, out object cached))
        return cached;

    var result = await ExecuteControllerActionAsync();
    memoryCache.Set(cacheKey, result, apiCfg.GetCacheOptions());
    return result;
}
return await ExecuteControllerActionAsync();
```

---
## 4. Error Handling
- Wrap `GetAppEndWebApiInfo` if middleware must not fail the pipeline.
- Provide sensible defaults when an `ApiConfiguration` is missing.
- Consider normalizing namespace via explicit route attributes.

---
## 5. Improvement Ideas
| Concern | Suggestion |
|---------|-----------|
| Naive namespace extraction | Use attribute metadata / route data tokens |
| Cache key collisions | Add API version or format to key |
| Missing configuration | Central fallback policy provider |

---
## 6. Limitations
- Namespace heuristic may mis-handle repeated segments.
- Assumes conventional `controller` / `action` route values exist.
