# DiagnosticsExtensions

Diagnostics and instrumentation helpers offering lightweight performance measurement, structured logging shortcuts, health probes, and runtime state export. Complements `DevelopmentExtensions` (developer-focused benchmarking) by providing production-safe minimal overhead utilities.

---
## Representative Capabilities
| Capability | Description |
|------------|-------------|
| MeasureOperation | Time an action / function and return duration + result |
| CaptureGCStats | Snapshot Gen0/1/2 collection counts & allocated bytes |
| CaptureThreadStats | Current thread count / pool metrics (if available) |
| ExportProcessMetrics | Consolidated object for health endpoint serialization |
| TrackSlowCall | Log calls exceeding threshold |

(Confirm against actual implementation.)

---
## Example
```csharp
var (result, elapsed) = DiagnosticsExtensions.MeasureOperation(() => Compute());
if (elapsed > TimeSpan.FromMilliseconds(250))
    logger.LogWarning("Slow Compute: {0} ms", elapsed.TotalMilliseconds);

var proc = DiagnosticsExtensions.ExportProcessMetrics();
```

---
## Guidance
- Keep measurement around hot paths minimal to avoid self-induced overhead.
- Centralize threshold configuration via `PowNetConfiguration` (e.g., `PowNet:Diagnostics:SlowCallMs`).

---
## Extension Ideas
| Area | Idea |
|------|------|
| Async support | Provide `MeasureOperationAsync` |
| Histogram integration | Emit metrics to monitoring backend (Prometheus/OpenTelemetry) |
| Allocation tracking | Track per-operation allocated bytes (GC sample) |

---
## Limitations
- High-frequency timing can distort performance (observer effect).
- Allocation snapshots coarse (rely on `GC.GetTotalMemory`).
