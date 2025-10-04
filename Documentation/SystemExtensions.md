# SystemExtensions

Helpers targeting system / environment operations: process info, environment variables, assembly metadata, and runtime metrics.

---
## Representative Methods
| Method | Purpose |
|--------|---------|
| GetEnvironmentVariableOrDefault(string key, string fallback) | Safe env var fetch |
| GetCurrentProcessInfo() | Snapshot of PID, memory, CPU time |
| GetAssemblyVersion(this Assembly asm) | Semantic version string |
| Is64BitProcessFast() | Quick IntPtr size check |
| GetMachineNameSafe() | Null-safe machine name |
| GetUptime() | TimeSpan since process start |

(Adjust per actual implementation.)

---
## Example
```csharp
var proc = SystemExtensions.GetCurrentProcessInfo();
Console.WriteLine(proc.WorkingSet);
```

---
## Guidance
- Avoid invoking heavy system calls on hot request paths; cache if needed.
- Expose metrics via diagnostics endpoint instead of ad-hoc logging for observability.

---
## Extension Ideas
| Idea | Benefit |
|------|--------|
| Cross-platform CPU usage normalization | Consistent metrics on Linux/Windows |
| Process health summary | Single object for uptime, threads, GC stats |
