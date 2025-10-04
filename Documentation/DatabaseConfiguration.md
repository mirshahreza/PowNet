# DatabaseConfiguration

Configuration model and helpers for database connectivity, retry policies, pooling and basic health checks.

---
## Representative Settings
| Key | Description |
|-----|-------------|
| ConnectionString | Primary connection string identifier/name |
| CommandTimeoutSeconds | Default command timeout |
| EnableRetry | Toggle transient retry logic |
| MaxRetryCount | Number of retries for transient failures |
| RetryBaseDelayMs | Initial backoff delay in milliseconds |
| EnableConnectionPooling | Allow pooling if provider supports |

---
## Usage Example
```csharp
var connStr = PowNetConfiguration.GetConnectionStringByName("MainDb");
int timeout = PowNetConfiguration.GetConfigValue("PowNet:Database:CommandTimeout", 30);
```

If a retry helper exists:
```csharp
var result = await RetryHelper.ExecuteAsync(() => RunQueryAsync(), maxRetries: dbCfg.MaxRetryCount);
```

---
## Guidance
- Keep retry windows short; long cumulative delays can trigger cascading failures.
- Centralize connection creation so metrics & tracing can be applied consistently.

---
## Extension Ideas
| Need | Idea |
|------|------|
| Circuit breaking | Integrate breaker to halt after sustained failures |
| Observability | Add connection attempt / retry metrics |
