# FeatureManager

Lightweight feature flag / toggle utilities for enabling or disabling functionality at runtime.

---
## Capabilities
- Check if a named feature is enabled
- Enable/disable at runtime (in-memory overrides)
- Optional evaluation context (user, role, environment) if implemented

---
## Representative Methods
| Method | Purpose |
|--------|---------|
| IsEnabled(string featureName) | Returns true if feature active |
| Enable(string featureName) | Turn on feature flag |
| Disable(string featureName) | Turn off feature flag |
| Evaluate(string featureName, object context) | Contextual rule evaluation (if provided) |

---
## Usage
```csharp
if (FeatureManager.IsEnabled("BetaUI"))
{
    RenderNewInterface();
}
```

---
## Guidance
- Persist long-lived flags outside process (config / db) for multi-instance consistency.
- Name features descriptively (e.g., `Orders.BatchExport`).

---
## Limitations
- Without persistence changes are lost on restart.
- Rule engine may be simplistic (add percentage rollout or user targeting as needed).
