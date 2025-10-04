# ControllerConfiguration

Container for per-controller API settings including a collection of `ApiConfiguration` entries and any shared defaults applied across actions.

---
## Representative Properties
| Property | Purpose |
|----------|---------|
| ControllerName | Logical controller identifier |
| NamespaceName | Grouping / route namespace |
| ApiConfigurations | List of endpoint (action) configs |
| DefaultCacheLevel | Fallback cache setting applied when an action has no explicit value |
| DefaultLogEnabled | Global logging toggle default |

(Adjust to actual code.)

---
## Lookup Pattern
```csharp
var ctrlCfg = ControllerConfiguration.GetConfig(nsName, controllerName);
var apiCfg = ctrlCfg.ApiConfigurations.FirstOrDefault(a => a.ApiName == action)
             ?? new ApiConfiguration { ApiName = action, CacheLevel = ctrlCfg.DefaultCacheLevel };
```

---
## Guidance
- Keep defaults minimal; override at action level only where behavior diverges.
- Validate uniqueness of `ApiName` entries to avoid ambiguous matches.

---
## Extension Ideas
| Area | Idea |
|------|------|
| Versioning | Include API version dimension |
| Rate limiting | Add per-action rate limit policy reference |
