using System.Collections;
using System.Data;
using System.Data.Common;
using FluentAssertions;
using PowNet.Common; 
using PowNet.Configuration;
using PowNet.Data;
using Xunit;

namespace PowNet.Test.Data;

/* -------------------------------------------------------------------------
 * Fresh rebuilt test file.
 * Provides a complete in‑memory / fake ADO.NET stack used to validate the
 * behaviors of DbCommandExecutor without touching a real database provider.
 * ------------------------------------------------------------------------- */

#region Fake ADO.NET Infrastructure

internal sealed class FakeDbConnection : DbConnection
{
    private ConnectionState _state = ConnectionState.Closed;
    public override string ConnectionString { get; set; } = string.Empty;
    public override string Database => "Fake";
    public override string DataSource => "FakeDS";
    public override string ServerVersion => "1.0";
    public override ConnectionState State => _state;
    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => new FakeDbTransaction(this, isolationLevel);
    public override void ChangeDatabase(string databaseName) { }
    public override void Close() => _state = ConnectionState.Closed;
    public override void Open() => _state = ConnectionState.Open;
    protected override DbCommand CreateDbCommand() => new FakeDbCommand(this);
}

internal sealed class FakeDbTransaction(FakeDbConnection conn, IsolationLevel iso) : DbTransaction
{
    protected override DbConnection DbConnection => conn;
    public override IsolationLevel IsolationLevel => iso;
    public override void Commit() { }
    public override void Rollback() { }
}

internal sealed class FakeDbCommand : DbCommand
{
    private readonly FakeDbConnection _conn;
    private readonly FakeParameterCollection _parameters = new();
    public FakeDbCommand(FakeDbConnection conn) => _conn = conn;

    public override string CommandText { get; set; } = string.Empty;
    public override int CommandTimeout { get; set; } = 30;
    public override CommandType CommandType { get; set; } = CommandType.Text;
    public override bool DesignTimeVisible { get; set; }
    public override UpdateRowSource UpdatedRowSource { get; set; }
    protected override DbConnection DbConnection { get => _conn; set { } }
    protected override DbParameterCollection DbParameterCollection => _parameters;
    protected override DbTransaction? DbTransaction { get; set; }

    public override void Cancel() { }
    public override void Prepare() { }
    protected override DbParameter CreateDbParameter() => new FakeParameter();

    public override int ExecuteNonQuery()
    {
        if (CommandText.Contains("FAIL", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Boom");
        return 42;
    }

    public override object? ExecuteScalar()
    {
        if (CommandText.Contains("FAIL_SCALAR", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("BoomScalar");
        return 7;
    }

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => new FakeReader();
}

internal sealed class FakeReader : DbDataReader
{
    private bool _read;
    public override int FieldCount => 1;
    public override bool HasRows => true;
    public override bool IsClosed => false;
    public override int RecordsAffected => 1;
    public override int Depth => 0;

    public override bool Read()
    {
        if (_read) return false; _read = true; return true;
    }
    public override bool NextResult() => false;

    public override object GetValue(int ordinal) => 99;
    public override int GetOrdinal(string name) => 0;
    public override string GetName(int ordinal) => "Col1";
    public override string GetDataTypeName(int ordinal) => typeof(int).Name;
    public override Type GetFieldType(int ordinal) => typeof(int);
    public override bool IsDBNull(int ordinal) => false;
    public override object this[int ordinal] => GetValue(ordinal);
    public override object this[string name] => GetValue(0);
    public override IEnumerator GetEnumerator() { yield return 99; }
    public override int GetInt32(int ordinal) => 99;

    // DataTable.Load relies on schema; provide minimal schema table.
    public override DataTable GetSchemaTable()
    {
        var schema = new DataTable();
        schema.Columns.Add("ColumnName", typeof(string));
        schema.Columns.Add("ColumnOrdinal", typeof(int));
        schema.Columns.Add("DataType", typeof(Type));
        schema.Columns.Add("AllowDBNull", typeof(bool));
        var r = schema.NewRow();
        r["ColumnName"] = "Col1";
        r["ColumnOrdinal"] = 0;
        r["DataType"] = typeof(int);
        r["AllowDBNull"] = false;
        schema.Rows.Add(r);
        return schema;
    }

    #region Unused overrides (simplified / stubbed)
    public override int VisibleFieldCount => FieldCount;
    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => throw new NotImplementedException();
    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => throw new NotImplementedException();
    public override string GetString(int ordinal) => GetValue(ordinal).ToString()!;
    public override long GetInt64(int ordinal) => 99;
    public override bool GetBoolean(int ordinal) => true;
    public override byte GetByte(int ordinal) => 0;
    public override char GetChar(int ordinal) => 'a';
    public override DateTime GetDateTime(int ordinal) => DateTime.UtcNow;
    public override decimal GetDecimal(int ordinal) => 99;
    public override double GetDouble(int ordinal) => 99;
    public override float GetFloat(int ordinal) => 99;
    public override Guid GetGuid(int ordinal) => Guid.Empty;
    public override short GetInt16(int ordinal) => 99;
    public override Type GetProviderSpecificFieldType(int ordinal) => GetFieldType(ordinal);
    public override object GetProviderSpecificValue(int ordinal) => GetValue(ordinal);
    public override int GetValues(object[] values) { values[0] = 99; return 1; }
    #endregion
}

internal sealed class FakeParameter : DbParameter
{
    public override DbType DbType { get; set; }
    public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;
    public override bool IsNullable { get; set; } = true;
    public override string ParameterName { get; set; } = string.Empty;
    public override string SourceColumn { get; set; } = string.Empty;
    public override object? Value { get; set; }
    public override bool SourceColumnNullMapping { get; set; }
    public override int Size { get; set; }
    public override void ResetDbType() { }
}

internal sealed class FakeParameterCollection : DbParameterCollection
{
    private readonly List<DbParameter> _list = new();
    public override int Count => _list.Count;
    public override object SyncRoot => ((ICollection)_list).SyncRoot!;
    public override int Add(object value) { _list.Add((DbParameter)value); return _list.Count - 1; }
    public override void AddRange(Array values) { foreach (var v in values) Add(v!); }
    public override void Clear() => _list.Clear();
    public override bool Contains(object value) => _list.Contains((DbParameter)value);
    public override bool Contains(string value) => _list.Exists(p => p.ParameterName == value);
    public override void CopyTo(Array array, int index) => _list.ToArray().CopyTo(array, index);
    public override IEnumerator GetEnumerator() => _list.GetEnumerator();
    public override int IndexOf(object value) => _list.IndexOf((DbParameter)value);
    public override int IndexOf(string parameterName) => _list.FindIndex(p => p.ParameterName == parameterName);
    public override void Insert(int index, object value) => _list.Insert(index, (DbParameter)value);
    public override void Remove(object value) => _list.Remove((DbParameter)value);
    public override void RemoveAt(int index) => _list.RemoveAt(index);
    public override void RemoveAt(string parameterName) { var idx = IndexOf(parameterName); if (idx >= 0) RemoveAt(idx); }
    protected override DbParameter GetParameter(int index) => _list[index];
    protected override DbParameter GetParameter(string parameterName) => _list[IndexOf(parameterName)];
    protected override void SetParameter(int index, DbParameter value) => _list[index] = value;
    protected override void SetParameter(string parameterName, DbParameter value) { var idx = IndexOf(parameterName); if (idx >= 0) _list[idx] = value; else _list.Add(value); }
}

internal sealed class FakeDataAdapter : DataAdapter
{
    public override int Fill(DataSet dataSet)
    {
        var t = new DataTable("T0");
        t.Columns.Add("Col1", typeof(int));
        var r = t.NewRow();
        r[0] = 99;
        t.Rows.Add(r);
        dataSet.Tables.Add(t);
        return 1;
    }
}

internal sealed class TestDbCommandExecutor : DbCommandExecutor
{
    public TestDbCommandExecutor() : base(new DatabaseConfiguration("Test", ServerType.MsSql, "Fake")) { }

    // Ensure base ctor uses our fake connection (CreateConnection is virtual in framework code).
    public override DbConnection CreateConnection() => new FakeDbConnection();
    public override DataAdapter CreateDataAdapter(DbCommand dbCommand) => new FakeDataAdapter();

    public override DbCommand CreateDbCommand(string commandText, DbConnection dbConnection, List<DbParameter>? dbParameters = null)
    {
        var cmd = new FakeDbCommand((FakeDbConnection)dbConnection) { CommandText = commandText };
        if (dbParameters != null)
            foreach (var p in dbParameters) cmd.Parameters.Add(p);
        return cmd;
    }

    public override DbParameter CreateParameter(string columnName, string columnType, int? columnSize = null, object? value = null)
    {
        var p = new FakeParameter { ParameterName = columnName, Value = value ?? DBNull.Value };
        if (!string.IsNullOrWhiteSpace(columnType) && Enum.TryParse(columnType, true, out DbType dbType)) p.DbType = dbType;
        if (columnSize.HasValue) p.Size = columnSize.Value;
        return p;
    }
}

#endregion

// -------------------------------------------------------------------------
// Tests
// -------------------------------------------------------------------------
public class DbCommandExecutorTests
{
    [Fact]
    public void ExecuteNonQuery_Should_Invoke_Hooks_And_Return_Value()
    {
        var db = new TestDbCommandExecutor();
        int before = 0; TimeSpan after = TimeSpan.Zero; int afterCnt = 0;
        db.OnBeforeExecute = _ => before++;
        db.OnAfterExecute = (_, ts) => { after = ts; afterCnt++; };

        int n = db.ExecuteNonQuery("SELECT 1");

        n.Should().Be(42);
        before.Should().Be(1);
        afterCnt.Should().Be(1);
        after.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task ExecuteScalarAsync_Should_Return_Value_And_Hooks()
    {
        var db = new TestDbCommandExecutor();
        int before = 0; int after = 0;
        db.OnBeforeExecute = _ => before++;
        db.OnAfterExecute = (_, __) => after++;

        var val = await db.ExecuteScalarAsync("SELECT 7");
        val.Should().Be(7);
        before.Should().Be(1);
        after.Should().Be(1);
    }

    [Fact]
    public void ExecuteDataTable_Should_Return_Row()
    {
        var db = new TestDbCommandExecutor();
        var dt = db.ExecuteDataTable("SELECT Col1 FROM Anything");
        dt.Rows.Count.Should().Be(1);
        dt.Columns.Count.Should().Be(1);
        dt.Rows[0][0].Should().Be(99);
    }

    [Fact]
    public void Transaction_Begin_Commit_Should_Toggle_State()
    {
        var db = new TestDbCommandExecutor();
        db.InTransaction.Should().BeFalse();
        db.BeginTransaction();
        db.InTransaction.Should().BeTrue();
        db.ExecuteNonQuery("UPDATE Something");
        db.CommitTransaction();
        db.InTransaction.Should().BeFalse();
    }

    [Fact]
    public void Transaction_Rollback_Should_Clear_State()
    {
        var db = new TestDbCommandExecutor();
        db.BeginTransaction();
        db.ExecuteNonQuery("UPDATE X");
        db.RollbackTransaction();
        db.InTransaction.Should().BeFalse();
    }

    [Fact]
    public void Execute_Should_Wrap_Exception()
    {
        var db = new TestDbCommandExecutor();
        Action act = () => db.ExecuteNonQuery("FAIL NOW");
        var ex = act.Should().Throw<Exception>().Which;
        ex.Message.Should().Contain("ToNonQueryFailed");
        (ex.Data["Query"]?.ToString() ?? string.Empty).Should().Contain("FAIL NOW");
    }

    [Fact]
    public void Dispose_Should_Close_Connection()
    {
        var db = new TestDbCommandExecutor();
        db.ExecuteNonQuery("SELECT 1");
        var connField = typeof(DbCommandExecutor).GetField("_dbConnection", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var conn = (DbConnection)connField.GetValue(db)!;
        conn.State.Should().Be(ConnectionState.Open);
        db.Dispose();
        conn.State.Should().Be(ConnectionState.Closed);
    }

    [Fact]
    public void CreateParameter_Should_Map_Type_And_Size()
    {
        var db = new TestDbCommandExecutor();
        var p = db.CreateParameter("@Id", "Int32", 8, 5);
        p.ParameterName.Should().Be("@Id");
        p.Value.Should().Be(5);
        p.DbType.Should().Be(DbType.Int32);
        p.Size.Should().Be(8);
    }
}
