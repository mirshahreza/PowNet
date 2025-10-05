# PowNet Data Layer (DbIO)

This document describes the data access layer built around the abstract `DbIO` facade. The current implementation includes `DbIOMsSql` (SQL Server provider). The design focuses on a thin, extensible, provider?agnostic execution surface over ADO.NET.

## Design Goals
- Single, consistent execution pipeline (sync + async)
- Centralized exception wrapping (rich context: SQL + parameters)
- Extensible hooks for logging / metrics (`OnBeforeExecute`, `OnAfterExecute`)
- Lightweight transaction handling (Begin / Commit / Rollback)
- Easy provider extension (minimal abstract surface)
- Backward compatibility with DataTable / DataSet usage while enabling modern patterns later

## Core Class: `DbIO`
Features:
- Connection management with lazy open safeguard (`EnsureConnectionOpen`)
- Generic execution wrappers: `Execute<T>` / `ExecuteAsync<T>` (stopwatch + hooks + error wrap)
- CRUD style helpers:
  - Sync: `ToDataSet`, `ToDataTables`, `ToDataTable`, `ToScalar`, `ToNonQuery`
  - Async: `ToDataSetAsync`, `ToDataTablesAsync`, `ToDataTableAsync`, `ToScalarAsync`, `ToNonQueryAsync`
  - Convenience aliases: `ExecuteScalar`, `ExecuteNonQuery`, `ExecuteDataTable` (+ async variants)
- Transaction state tracking via private `_transaction` and `InTransaction`
- Parameter serialization for diagnostics (truncates long values >256 chars)
- Abstract SQL template surface for provider specialization

### Execution Hooks
```csharp
using var db = DbIO.Instance("DefaultConnection");
db.OnBeforeExecute = cmd => logger.LogInformation("SQL => {Sql}", cmd.CommandText);
db.OnAfterExecute  = (cmd, elapsed) => metrics.Timer("sql.exec").Record(elapsed);
```

### Transactions
```csharp
using var db = DbIO.Instance();
try
{
    db.BeginTransaction();
    db.ExecuteNonQuery("UPDATE Accounts SET Balance = Balance - 100 WHERE Id = @Id", new(){ db.CreateParameter("@Id","Int",null,1)});
    db.ExecuteNonQuery("UPDATE Accounts SET Balance = Balance + 100 WHERE Id = @Id", new(){ db.CreateParameter("@Id","Int",null,2)});
    db.CommitTransaction();
}
catch
{
    db.RollbackTransaction();
    throw;
}
```

### Async Usage
```csharp
int affected = await db.ExecuteNonQueryAsync(
    "UPDATE Users SET LastLogin = GETUTCDATE() WHERE Id = @Id",
    new(){ db.CreateParameter("@Id","Int",null,7) });
```

## Provider: `DbIOMsSql`
Responsibilities:
- Create and open a `SqlConnection`
- Build `SqlCommand` and auto?inject missing parameters discovered in the SQL text
- Supply SQL templates per `QueryType` (Create / Read / Update / Delete / Functions / Procedures)
- Translate database parameter metadata to C# method signature fragments (`DbParamToCSharpInputParam`)
- Compile where comparison clauses (StartsWith, EndsWith, Contains, etc.)

## Adding a New Provider (e.g., PostgreSQL)
1. Implement subclass (e.g., `DbIOPostgres`).
2. Override abstract members: connection, command, adapter, parameter creation; template retrieval; clause compiler; parameter signature mapping.
3. Add provider case to `DbIO.Instance`.
4. Add focused tests (pattern from `DbIOTests`).

## Error Wrapping
Codes like `ToNonQueryFailed`, `ToScalarFailed` embed:
- Original message
- SQL command
- JSON?like serialized parameters

## DataSet Multi?Result Example
```csharp
var tables = db.ToDataSet(
    "SELECT * FROM Roles; SELECT * FROM Permissions;",
    tableNames: new(){"Roles","Permissions"});
var roles = tables["Roles"]; // DataTable
```

## Backlog / Future Enhancements
- `IDbIO` interface for DI
- Streaming reader helper: `ExecuteReaderFunc(Func<DbDataReader,T>)`
- Parameter normalization helpers
- Template caching (`ConcurrentDictionary`)
- POCO mapping (reflection / source generator)
- Retry / resiliency policies
- Bulk operations (e.g., `SqlBulkCopy`)
- Metrics enrichment (row count, parameter count, exceptions)

## Testing Strategy
Fake ADO.NET types validate:
- Hook invocation order
- Sync vs async parity
- Transaction state transitions
- Exception wrapping semantics
- Disposal (connection closed, transaction cleared)

---
Document auto?generated for PowNet Data layer.
