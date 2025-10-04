# PowNetConfiguration

High-level runtime configuration manager for the PowNet framework.

Provides:
- Hierarchical key access using `Section:SubSection:Key` syntax
- Environment awareness (Production / Development / Staging) with automatic detection
- In?memory runtime overrides (highest precedence) via `SetConfigValue`
- Environment variable binding (`PowNet:Security:RequireHttps` -> `PowNet__Security__RequireHttps`)
- JSON configuration loading (base + optional environment file `appsettings.{Env}.json`)
- Secret helper (`GetSecretValue`) preferring environment variables
- Backup / restore of active configuration file
- Validation (paths, security, performance, connection strings)
- Internal caching (5?minute TTL) + explicit `RefreshSettings`

## Method Reference
Below each method lists signature, purpose, parameters and a concrete usage snippet.

### GetConfigValue<T>(string key, T defaultValue = default!)
Fetches a configuration value resolved through precedence chain (runtime override ? env var ? env json ? base json ? fallback).

Parameters:
- `key`: Hierarchical key using ':' separators (e.g. `PowNet:Database:CommandTimeout`).
- `defaultValue`: Value to return if resolution or conversion fails.

Returns: `T`

Example:
```csharp
int timeout = PowNetConfiguration.GetConfigValue("PowNet:Database:CommandTimeout", 30);
bool pooling = PowNetConfiguration.GetConfigValue("PowNet:Database:EnableConnectionPooling", true);
```

### SetConfigValue(string key, object value)
Adds/updates a runtime override (in?memory) and mutates the internal JSON tree so subsequent `Save()` will persist.

Example:
```csharp
PowNetConfiguration.SetConfigValue("PowNet:Features:NewUI", true);
bool enabled = PowNetConfiguration.GetConfigValue("PowNet:Features:NewUI", false); // true
```

### GetSecretValue(string key, string defaultValue = "")
Returns first available value from: runtime override ? environment variable ? config ? provided default.

Example:
```csharp
string secret = PowNetConfiguration.GetSecretValue("PowNet:EncryptionSecret", "dev-insecure");
```

### Save()
Serializes current JSON tree (including overrides) to active config file (`appsettings.{Env}.json` if present else base) and refreshes caches.

Example:
```csharp
PowNetConfiguration.SetConfigValue("PowNet:Logging:RetentionDays", 14);
PowNetConfiguration.Save();
```

### RefreshSettings()
Clears in?memory caches causing next access to reload from disk.
```csharp
PowNetConfiguration.RefreshSettings();
```

### CreateConfigurationBackup()
Creates timestamped copy of active configuration in `BackupPath` (ensures base exists). Returns path.

Example:
```csharp
var backupPath = PowNetConfiguration.CreateConfigurationBackup();
// ... changes ...
PowNetConfiguration.RestoreConfigurationFromBackup(backupPath);
```

### RestoreConfigurationFromBackup(string backupPath)
Overwrites active config file with the specified backup and refreshes settings.

Example:
```csharp
PowNetConfiguration.RestoreConfigurationFromBackup(backupPath);
```

### ValidateConfiguration()
Runs validation (paths, security, performance, connection strings). Returns `ConfigurationValidationResult` with `Errors` & `Warnings`.

Example:
```csharp
var result = PowNetConfiguration.ValidateConfiguration();
if (!result.IsValid)
{
    foreach (var e in result.Errors) Console.WriteLine(e);
}
```

### GetConnectionStringByName(string name)
Resolves connection string via env overrides ? JSON; throws if missing.

Example:
```csharp
var mainCs = PowNetConfiguration.GetConnectionStringByName("MainDb");
```

### GetConnectionStrings()
Enumerates all JSON connection strings.

Example:
```csharp
foreach (var kv in PowNetConfiguration.GetConnectionStrings())
    Console.WriteLine($"{kv.Key} = {kv.Value}");
```

## Environment Helpers
Properties:
- `Environment` (changing triggers `RefreshSettings`)
- `IsDevelopment | IsProduction | IsStaging`

Example:
```csharp
var prev = PowNetConfiguration.Environment;
PowNetConfiguration.Environment = "Development"; // enable dev-specific behavior
// ...
PowNetConfiguration.Environment = prev;
```

## Validation Result Object
`ConfigurationValidationResult`:
- `bool IsValid`
- `List<string> Errors`
- `List<string> Warnings`

## Feature Toggle Pattern
```csharp
bool IsOn(string feature) => PowNetConfiguration.GetConfigValue($"PowNet:Features:{feature}", false);
PowNetConfiguration.SetConfigValue("PowNet:Features:Beta", true);
Console.WriteLine(IsOn("Beta")); // True
```

## Error Handling
- Retrieval: Failures log debug info & return fallback.
- Mutation: Throws `PowNetConfigurationException` enriched via fluent `.AddParam`.

Example:
```csharp
try
{
    PowNetConfiguration.SetConfigValue("Invalid::Key", 1);
}
catch (PowNetConfigurationException ex)
{
    Console.WriteLine(ex.Message);
}
```

## Precedence Recap
`Runtime Override > Environment Variables > Env JSON > Base JSON > Default Parameter`

## Performance Tips
- Minimize frequent `RefreshSettings()`; rely on TTL caching.
- Group multiple `SetConfigValue` calls before `Save()`.

## Security Tips
- Provide sensitive secrets exclusively via environment variables in production.
- Avoid committing real secrets into repository JSON files.

---
*Document curated manually (not auto-generated).*
