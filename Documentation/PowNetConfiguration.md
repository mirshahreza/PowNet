# PowNetConfiguration

Comprehensive runtime configuration manager for the PowNet framework. It provides:
- Hierarchical key access using a `Section:SubSection:Key` syntax
- Environment awareness (Production / Development / Staging) with auto detection
- Runtime overrides (highest precedence, in?memory)
- Environment variable binding (e.g. `PowNet:Security:RequireHttps` -> `PowNet__Security__RequireHttps`)
- JSON appsettings loading with optional environment-specific file (e.g. `appsettings.Development.json`)
- Secret resolution helper
- Backup / restore of the active configuration file
- Validation (paths, security, performance, connection strings)
- Internal caching with transparent refresh

## Precedence Order (highest ? lowest)
1. Runtime overrides (`SetConfigValue` / `_runtimeOverrides`)
2. Environment variables
3. Environment specific JSON (if exists)
4. Base `appsettings.json`
5. Supplied default value parameter

## Thread Safety
All mutable shared state (`_appsettings`, `_configCache`, `_runtimeOverrides`, `_currentEnvironment`) is protected via locks or concurrent collections.

---
## Properties (Selected)
| Property | Type | Description |
|----------|------|-------------|
| Environment | string | Current environment (lazy detected; assigning triggers refresh). |
| IsDevelopment / IsProduction / IsStaging | bool | Convenience flags. |
| WorkspacePath / LogsPath / TempPath / BackupPath | string | Derived or configured filesystem paths. |
| JwtExpirationHours | int | Security JWT expiration setting. |
| RequireHttps | bool | Defaults to true in Production unless overridden. |
| LogLevel | string | Defaults to Debug (Development) else Information. |

(Additional strongly?typed getters follow same pattern.)

---
## Core Methods

### GetConfigValue<T>(string key, T defaultValue = default!)
Fetch a configuration value and convert to `T` with fallback.

Parameters:
- key: Hierarchical key using ':' separators.
- defaultValue: Returned when no value is resolved or conversion fails.

Returns: Value of type `T` resolved via precedence order.

Example:
```csharp
int timeout = PowNetConfiguration.GetConfigValue("PowNet:Database:CommandTimeout", 30);
bool pooling = PowNetConfiguration.GetConfigValue("PowNet:Database:EnableConnectionPooling", true);
```

### SetConfigValue(string key, object value)
Adds/updates a runtime override (in memory) and updates the in-memory JSON tree to keep Save() coherent.

Effect: Invalidates cached entries for the key prefix.

Example:
```csharp
PowNetConfiguration.SetConfigValue("PowNet:Features:NewDashboard", true);
bool enabled = PowNetConfiguration.GetConfigValue("PowNet:Features:NewDashboard", false); // true
```

### GetSecretValue(string key, string defaultValue = "")
Returns first available secret (runtime override > environment variable > config > fallback).

Example:
```csharp
string encKey = PowNetConfiguration.GetSecretValue("PowNet:EncryptionSecret", "unsafe-dev-key");
```

### Save()
Serializes the current effective `AppSettings` JSON tree back to the active configuration file (base or environment?specific) and refreshes caches.

Example:
```csharp
PowNetConfiguration.SetConfigValue("PowNet:Logging:RetentionDays", 14);
PowNetConfiguration.Save();
```

### RefreshSettings()
Clears in-memory cached JSON + value cache; next access reloads from disk.
```csharp
PowNetConfiguration.RefreshSettings();
```

### CreateConfigurationBackup()
Copies the active config file to `BackupPath` with a timestamped name. Ensures base file exists.

Returns: Full path to backup file.

Example:
```csharp
var backup = PowNetConfiguration.CreateConfigurationBackup();
// ... risky changes ...
PowNetConfiguration.RestoreConfigurationFromBackup(backup);
```

### RestoreConfigurationFromBackup(string backupPath)
Overwrites the active configuration file with the specified backup and refreshes settings.

### ValidateConfiguration()
Performs structural & policy validation (paths, connection strings, security, performance). Returns `ConfigurationValidationResult`.

Example:
```csharp
var result = PowNetConfiguration.ValidateConfiguration();
if(!result.IsValid)
{
    foreach(var e in result.Errors) Console.WriteLine(e);
}
```

### GetConnectionStringByName(string name)
Resolves connection string (environment variable overrides) else JSON. Throws if not found.

### GetConnectionStrings()
Enumerates all configured connection strings (JSON section required or exception thrown).

---
## Internal Helpers (Overview)
| Method | Purpose |
|--------|---------|
| DetermineEnvironment() | Probe env vars then base config, default Production. |
| LoadAppSettings() | Load JSON (env-specific if present). |
| ConvertValue<T>() | Type conversion for common primitives & TimeSpan. |
| ShouldRefreshConfig() | TTL-based cache invalidation logic. |
| GetNestedValue(JsonNode, key) | Traverses JSON using ':' segments. |

---
## Validation Internals (Highlights)
- Path validation: full path resolution + existence warnings
- Security: EncryptionSecret length & placeholder detection
- Performance: Ensures positive limits

---
## Example: Feature Toggle Pattern
```csharp
bool IsFeatureOn(string featureKey) =>
    PowNetConfiguration.GetConfigValue($"PowNet:Features:{featureKey}", false);

PowNetConfiguration.SetConfigValue("PowNet:Features:Alpha", true);
Console.WriteLine(IsFeatureOn("Alpha")); // True
```

## Example: Environment Switch in Tests
```csharp
var oldEnv = PowNetConfiguration.Environment;
PowNetConfiguration.Environment = "Development"; // triggers refresh
// run dev-only code
PowNetConfiguration.Environment = oldEnv; // revert
```

---
## Error Handling
Configuration access attempts are resilient: on exception during retrieval a default value is returned (and error logged with Debug.WriteLine). Mutation errors raise `PowNetConfigurationException` including contextual parameters via fluent `AddParam`.

---
## Tips
- Prefer secrets via environment variables in production (avoid persisting in JSON)
- Use runtime overrides for test isolation without needing temp files
- Call `Save()` only when you truly want to persist overrides
- Avoid overusing frequent `RefreshSettings()`; rely on caching window for performance.
