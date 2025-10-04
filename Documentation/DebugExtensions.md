# DebugExtensions

Developer-focused helpers for conditional debug output, object inspection, quick timing, and assertion-style validations used during development or diagnostics.

---
## Representative Methods
| Method | Purpose |
|--------|---------|
| Dump(this object obj, string? label = null) | Write object (JSON or reflection) to debug output |
| Measure(this Action action, string? name = null) | Time synchronous action and log duration |
| Assert(bool condition, string message) | Throw in Debug/Development if condition false |
| TraceValue<T>(this T value, string? label = null) | Log and return value for fluent chains |

(Adapt to actual code.)

---
## Example
```csharp
using var _ = (() => Work()).Measure("WorkBlock");
var user = dto.TraceValue("User DTO");
```

---
## Notes
- Guarded so production builds minimize overhead.
- Prefer structured logging for production telemetry; these helpers are transient.

---
## Extension Ideas
| Idea | Benefit |
|------|---------|
| Integration with PowNetLogger scopes | Correlate debug traces |
| Conditional compilation symbol hooks | Fine-grained enable flags |
