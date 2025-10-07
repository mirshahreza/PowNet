# DbCommandExecutor Design Reference

## Summary
`DbCommandExecutor` (formerly `DbIO`) abstracts repetitive ADO.NET patterns (open connection, parameter injection, timing, exception enrichment) behind a consistent API.

## Execution Flow
```
PrepareCommand -> OnBeforeExecute -> Execute (ADO.NET) -> OnAfterExecute -> Return Result
                              \-> (Exception) -> Wrap (PowNetException)
```

## Key Components
| Area | Responsibility |
|------|----------------|
| Connection | Created by provider; ensured open on demand |
| Transactions | Simple stateful wrapper; ambient assignment in `PrepareCommand` |
| Hooks | Delegates: `OnBeforeExecute`, `OnAfterExecute` |
| Wrappers | `Execute<T>` / `ExecuteAsync<T>` centralize timing + error handling |
| Diagnostics | Parameter serialization (length truncated) + error codes |

## Async Strategy
Native async for command execution; `Task.Run` only for DataAdapter `Fill` (legacy support).

## Provider Extension Checklist
1. Inherit from `DbCommandExecutor`.
2. Implement abstract members (connection, command, parameters, optional templates / clause compiler / param-to-C# mapper if added).
3. Register in `DbCommandExecutor.Instance` switch.
4. Add tests (fake connection pattern).

## Error Codes
Examples: `ToDataSetFailed`, `ToDataTablesFailed`, `ToNonQueryFailed`, `ToScalarFailed`, `ToNonQueryAsyncFailed`.

## Backlog Ideas
- Interface extraction (`IDbCommandExecutor`)
- Streaming reader delegate executor
- Retry / circuit-breaker integration
- Bulk copy operations
- Source-generated result mapping
- Template caching layer
- Structured metrics (latency histograms, row counts)

## Security Notes
`DbCommandExecutor` does not sanitize SQL. Always parameterize user input with `CreateParameter`.

## Disposal
Implements both `IDisposable` and `IAsyncDisposable`; async disposal currently a no-op wrapper (ADO.NET typically sync only).

---
Design reference after rename.
