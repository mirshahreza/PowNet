# DbIO Usage

## Scalar Query
```csharp
using var db = DbIO.Instance("DefaultConnection");
int total = Convert.ToInt32(db.ExecuteScalar("SELECT COUNT(1) FROM Users") ?? 0);
```

## DataTable
```csharp
var dt = db.ExecuteDataTable("SELECT * FROM Users WHERE IsActive = 1");
```

## Parameters
```csharp
var pars = new List<DbParameter>{ db.CreateParameter("@Id","Int",null,5) };
var user = db.ExecuteDataTable("SELECT * FROM Users WHERE Id = @Id", pars);
```

## Transaction
```csharp
try
{
    db.BeginTransaction();
    db.ExecuteNonQuery("UPDATE Accounts SET Balance = Balance - 10 WHERE Id=1");
    db.ExecuteNonQuery("UPDATE Accounts SET Balance = Balance + 10 WHERE Id=2");
    db.CommitTransaction();
}
catch
{
    db.RollbackTransaction();
    throw;
}
```

## Async
```csharp
int affected = await db.ExecuteNonQueryAsync(
    "UPDATE Users SET LastLogin = GETUTCDATE() WHERE Id=@Id",
    new(){ db.CreateParameter("@Id","Int",null,7) });
```

## Logging Hooks
```csharp
db.OnBeforeExecute = c => logger.LogDebug("SQL => {Sql}", c.CommandText);
db.OnAfterExecute  = (c, t) => logger.LogInformation("Done in {Ms} ms", t.TotalMilliseconds);
```

## Multiple Result Sets
```csharp
var tables = db.ToDataSet(
    "SELECT * FROM Roles; SELECT * FROM Permissions;",
    tableNames: new(){"Roles","Permissions"});
var roles = tables["Roles"]; // DataTable
```

## Error Handling
```csharp
try { db.ExecuteNonQuery("FAIL SELECT"); }
catch (Exception ex) { /* ex.Message includes failure code + query */ }
```

## Provider Skeleton
```csharp
public sealed class DbIOPostgres : DbIO
{
    public DbIOPostgres(DatabaseConfiguration c) : base(c) {}
    public override DbConnection CreateConnection() => new NpgsqlConnection(DbConf.ConnectionString);
    // implement other abstract members...
}
```

---
Usage reference.
