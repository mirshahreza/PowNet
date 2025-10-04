# ConfigurationExtensions

Helpers for binding strongly typed configuration objects, comparing versions, generating diffs, and mapping configuration sections.

---
## Representative Methods
| Method | Purpose |
|--------|---------|
| BindConfiguration<T>(this string sectionPath, T instance) | Populate instance from hierarchical keys |
| CompareConfigurations<T>(T oldCfg, T newCfg) | Produce structural diff (changed/added/removed) |
| ToDictionaryFlat(object cfg) | Flatten object graph to key/value pairs |

(Adjust per actual implementation.)

---
## Example
```csharp
var cfg = "PowNet:Sample".BindConfiguration(new SampleConf());
var diff = ConfigurationExtensions.CompareConfigurations(oldCfg, newCfg);
foreach(var change in diff.Changes) Console.WriteLine(change);
```

---
## Diff Semantics (Typical)
- Added: key not present in old
- Removed: key present in old not new
- Modified: value changed (string comparison or primitive equality)

---
## Guidance
- Prefer immutable config objects after startup; use change tokens only when necessary.
- Provide validation after binding (e.g., FluentValidation or custom checks).

---
## Extension Ideas
| Need | Idea |
|------|------|
| Typed diff | Generate strongly typed change events |
| Sensitive fields | Mask values in diff output |
