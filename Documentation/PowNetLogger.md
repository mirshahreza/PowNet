# PowNetLogger

Lightweight structured logging facility with pluggable targets, category-based loggers, scoped measurement, performance helpers, and conditional logging utilities. Designed to operate without external dependencies while allowing extension to production log pipelines.

---
## 1. Core Concepts
- Global registry maps category name ? `Logger` instance (per category singleton)
- Pluggable `ILogTarget` instances receive `LogEntry` objects
- Global log enable/disable + global minimum level filtering
- Environment-aware initialization (console + file + structured targets)
- Structured logging: strongly typed objects converted to property bags
- Performance helpers (scopes, measurement, per-operation timing)

---
## 2. Initialization & Configuration
### PowNetLogger.Initialize()
Sets up default targets per environment:
- Development: console (colored) + Debug level
- Production: structured target + Warning level
- File target if `PowNetConfiguration.EnableFileLogging == true`
```csharp
PowNetLogger.Initialize();
```

### PowNetLogger.AddTarget(ILogTarget target)
Adds a custom destination (thread-safe list append).
```csharp
PowNetLogger.AddTarget(new ConsoleLogTarget());
```

### PowNetLogger.SetGlobalLogLevel(LogLevel level)
Sets minimum accepted level globally.
```csharp
PowNetLogger.SetGlobalLogLevel(LogLevel.Trace);
```

### PowNetLogger.SetEnabled(bool enabled)
Global on/off switch.
```csharp
PowNetLogger.SetEnabled(false); // mute all logging
```

---
## 3. Obtaining a Logger
### PowNetLogger.GetLogger(string category)
```csharp
var log = PowNetLogger.GetLogger("Billing");
```
### PowNetLogger.GetLogger<T>()
Uses type name as category.
```csharp
var log = PowNetLogger.GetLogger<MyService>();
```

---
## 4. Basic Logging Methods (Logger)
| Method | Description |
|--------|-------------|
| LogTrace/LogDebug/LogInformation | Verbose to informational messages |
| LogWarning | Potential problem |
| LogError | Recoverable error |
| LogCritical | Severe failure |
| LogException | Error with exception metadata |

Example:
```csharp
log.LogInformation("Processing order {0}", orderId);
try { Save(); } catch(Exception ex) { log.LogException(ex, "Failed saving order {0}", orderId); }
```

---
## 5. Structured Logging
### LogStructured(LogLevel level, string messageTemplate, object? data = null)
Builds log entry; if `data` is an object its public props become properties.
```csharp
log.LogStructured(LogLevel.Information, "Order processed", new { OrderId = id, Total = total });
```

### LogException(Exception exception, string? message = null, params object[] args)
Captures stack trace & exception type.
```csharp
log.LogException(ex, "Failure executing job {0}", jobId);
```

---
## 6. Scopes & Performance
### BeginScope(string name, object? data = null)
Returns an `IDisposable` scope (debug logs start/stop and elapsed ms).
```csharp
using (log.BeginScope("ImportCsv", new { rows }))
{
    ProcessRows(rows);
}
```

### LogPerformance(string operation, TimeSpan duration, object? data = null)
Explicit performance event.
```csharp
var sw = Stopwatch.StartNew();
Run();
sw.Stop();
log.LogPerformance("Run", sw.Elapsed, new { items });
```

### MeasurePerformance / MeasurePerformanceAsync
Wraps call measuring duration + logs performance or exception.
```csharp
var res = log.MeasurePerformance("Calc", () => Compute());
var dto = await log.MeasurePerformanceAsync("CallApi", async () => await Invoke());
```

---
## 7. Conditional Logging
### LogIf(bool condition, LogLevel level, string message, params object[] args)
Logs only when condition true.
```csharp
log.LogIf(isVerbose, LogLevel.Debug, "Detailed state {0}", state);
```
Convenience variants: `LogDebugIf`, `LogWarningIf`.

---
## 8. Logging Extensions (LoggingExtensions)
Augments `Logger` for common patterns:
- `LogMethodEntry(parameters?)` / `LogMethodExit(result?)`
- `LogHttpRequest(method, path, statusCode, duration, additionalData?)`
- `LogDatabaseOperation(operation, query?, duration?, affectedRows?)`
- `LogBusinessOperation(operation, success, data?, errorMessage?)`

Example:
```csharp
log.LogHttpRequest("GET", "/api/users", 200, TimeSpan.FromMilliseconds(35));
log.LogBusinessOperation("UserRegistration", success: true, data: new { userId });
```

---
## 9. Targets
### ConsoleLogTarget
Color-coded console output (mapping LogLevel ? ConsoleColor).

### FileLogTarget
Appends line-delimited JSON entries to daily files (`PowNet-yyyyMMdd.log`) with size-based rotation (using config `MaxLogFileSizeBytes`).
```csharp
PowNetLogger.AddTarget(new FileLogTarget(PowNetConfiguration.LogsPath));
```

### StructuredLogTarget
Writes structured JSON to `Debug.WriteLine` (intended adapter to external systems: ELK, Splunk). Extend to push to external sink.

### Custom Target
Implement `ILogTarget.WriteLog(LogEntry entry)` and add via `AddTarget`.

---
## 10. Data Structures
### LogEntry
| Field | Meaning |
|-------|--------|
| Timestamp | UTC timestamp |
| Level | Log severity |
| Category | Logger category |
| Message | Formatted message |
| ThreadId / ProcessId | Execution context IDs |
| Exception | Optional exception object |
| Properties | Structured key/value data |

### LogLevel
`Trace, Debug, Information, Warning, Error, Critical` (ascending severity).

---
## 11. Formatting & Safety
Messages use `string.Format`. On `FormatException`, fallback concatenates raw args (avoids throwing within logging path).

---
## 12. Usage Scenario
```csharp
PowNetLogger.Initialize();
var logger = PowNetLogger.GetLogger("Orders");
using(logger.BeginScope("ProcessOrder", new { orderId }))
{
    logger.LogInformation("Start processing order {0}", orderId);
    var total = logger.MeasurePerformance("CalcTotal", () => Calc(orderId));
    logger.LogStructured(LogLevel.Information, "OrderComplete", new { orderId, total });
}
```

---
## 13. Best Practices
- Limit structured object size (avoid large collections)
- Add environment-specific correlation (e.g., request ID) to `Properties` via custom target wrapper
- Use `LogIf` to skip expensive message building in hot paths
- For high-volume scenarios, batch or async-dispatch targets

---
## 14. Limitations
- No async buffering / batching out-of-box
- File target rotation only size-based (no retention purge)
- Structured target is illustrative (not persistent)

---
*Manual documentation.*
