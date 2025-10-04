# EnvironmentManager

Utilities for working with runtime environment settings (e.g., Development, Staging, Production) and detecting container / cloud hosting context.

---
## Features
- Load environment-specific JSON: `LoadEnvironmentConfig(name)` (maps to `appsettings.{name}.json` if present)
- Get / set current environment (`PowNetConfiguration.Environment`)
- Environment detection helpers: `IsRunningInDocker()`, `IsRunningInKubernetes()`, `IsRunningInAzure()`, `IsRunningInAWS()`, `IsRunningInGoogleCloud()` (heuristic via env vars / file presence)
- Validation: `ValidateEnvironment()` returns issues for unsupported or inconsistent setups

---
## Example
```csharp
PowNetConfiguration.Environment = "Staging"; // influences config resolution
EnvironmentManager.LoadEnvironmentConfig("Staging");

bool inDocker = EnvironmentManager.IsRunningInDocker();
var validation = EnvironmentManager.ValidateEnvironment();
if(!validation.IsValid)
{
    foreach(var issue in validation.Issues) Console.WriteLine(issue.Description);
}
```

---
## Notes
- Changing environment at runtime should be rare; prefer setting before service start.
- Cloud detection heuristics may produce false positives; refine as required.

---
## Extension Ideas
| Area | Idea |
|------|------|
| Telemetry | Emit structured event when environment changes |
| Policy | Restrict certain features outside Development |
