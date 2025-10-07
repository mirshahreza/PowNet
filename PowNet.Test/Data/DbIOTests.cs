using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using FluentAssertions;
using PowNet.Configuration;
using PowNet.Data;
using PowNet.Common;
using Xunit;

namespace PowNet.Test.Data
{
    // Fake infrastructure -----------------------------------------------------
    internal sealed class FakeDbConnection : DbConnection
    {
        private ConnectionState _state = ConnectionState.Closed;
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => new FakeDbTransaction(this, isolationLevel);
        public override void Close() => _state = ConnectionState.Closed;
        public override void ChangeDatabase(string databaseName) { }
        public override void Open() => _state = ConnectionState.Open;
        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "Fake";
        public override ConnectionState State => _state;
        public override string DataSource => "FakeDS";
        public override string ServerVersion => "1.0";
        protected override DbCommand CreateDbCommand() => new FakeDbCommand(this);
    }

    internal sealed class FakeDbTransaction : DbTransaction
    {
        private readonly FakeDbConnection _conn;
        private readonly IsolationLevel _iso;
        public FakeDbTransaction(FakeDbConnection conn, IsolationLevel iso) { _conn = conn; _iso = iso; }
        public override void Commit() { }
        protected override DbConnection DbConnection => _conn;
        public override IsolationLevel IsolationLevel => _iso;
        public override void Rollback() { }
    }

    internal sealed class FakeDbCommand : DbCommand
    {
        private readonly FakeDbConnection _conn;
        private readonly FakeParameterCollection _parameters = new();
        public FakeDbCommand(FakeDbConnection conn) { _conn = conn; }
        public override string CommandText { get; set; } = string.Empty;
        public override int CommandTimeout { get; set; } = 30;
        public override CommandType CommandType { get; set; } = CommandType.Text;
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection DbConnection { get => _conn; set { } }
        protected override DbParameterCollection DbParameterCollection => _parameters;
        protected override DbTransaction? DbTransaction { get; set; }
        public override void Cancel() { }
        public override int ExecuteNonQuery()
        {
            if (CommandText.Contains("FAIL")) throw new InvalidOperationException("Boom");
            return 42;
        }
        public override object? ExecuteScalar()
        {
            if (CommandText.Contains("FAIL_SCALAR")) throw new InvalidOperationException("BoomScalar");
            return 7;
        }
        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => new FakeParameter();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => new FakeReader();
    }

    internal sealed class FakeReader : DbDataReader
    {
        private bool _read = false;
        public override bool Read() { if (_read) return false; _read = true; return true; }
        public override int FieldCount => 1;
        public override object GetValue(int ordinal) => 99;
        public override string GetName(int ordinal) => "Col1";
        public override int GetOrdinal(string name) => 0;
        public override bool HasRows => true;
        public override bool IsDBNull(int ordinal) => false;
        public override int RecordsAffected => 1;
        public override bool IsClosed => false;
        public override int Depth => 0;
        public override bool NextResult() => false;
        public override object this[int ordinal] => GetValue(ordinal);
        public override object this[string name] => GetValue(0);
        public override string GetDataTypeName(int ordinal) => typeof(int).Name;
        public override Type GetFieldType(int ordinal) => typeof(int);
        public override IEnumerator GetEnumerator() { yield return 99; }
        public override int GetInt32(int ordinal) => 99;
        #region NotImplemented members
        public override int VisibleFieldCount => FieldCount; public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => throw new NotImplementedException(); public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => throw new NotImplementedException(); public override string GetString(int ordinal) => GetValue(ordinal).ToString()!; public override long GetInt64(int ordinal) => 99; public override bool GetBoolean(int ordinal) => true; public override byte GetByte(int ordinal) => 0; public override char GetChar(int ordinal) => 'a'; public override DateTime GetDateTime(int ordinal) => DateTime.UtcNow; public override decimal GetDecimal(int ordinal) => 99; public override double GetDouble(int ordinal) => 99; public override float GetFloat(int ordinal) => 99; public override Guid GetGuid(int ordinal) => Guid.Empty; public override short GetInt16(int ordinal) => 99; public override Type GetProviderSpecificFieldType(int ordinal) => GetFieldType(ordinal); public override object GetProviderSpecificValue(int ordinal) => GetValue(ordinal); public override int GetValues(object[] values) { values[0] = 99; return 1; }
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
            DataTable t = new("T0");
            t.Columns.Add("Col1", typeof(int));
            var row = t.NewRow();
            row[0] = 99;
            t.Rows.Add(row);
            dataSet.Tables.Add(t);
            return 1;
        }
    }

    internal sealed class TestDbIO : DbIO
    {
        public TestDbIO() : base(new DatabaseConfiguration("Test", ServerType.MsSql, "Fake")) { }
        public List<DbCommand> ExecutedCommands { get; } = new();
        public override DbConnection CreateConnection() => new FakeDbConnection();
        public override DbCommand CreateDbCommand(string commandText, DbConnection dbConnection, List<DbParameter>? dbParameters = null)
        {
            var cmd = (FakeDbCommand)((FakeDbConnection)dbConnection).CreateCommand();
            cmd.CommandText = commandText;
            if (dbParameters is not null)
            {
                foreach (var p in dbParameters) cmd.Parameters.Add(p);
            }
            return cmd;
        }
        public override DataAdapter CreateDataAdapter(DbCommand dbCommand) => new FakeDataAdapter();
        public override DbParameter CreateParameter(string columnName, string columnType, int? columnSize = null, object? value = null) => new FakeParameter { ParameterName = columnName, Value = value };
    }

    // Tests -------------------------------------------------------------------
    public class DbIOTests
    {
        [Fact]
        public void ExecuteNonQuery_Should_Invoke_Hooks_And_Return_Value()
        {
            var db = new TestDbIO();
            int before = 0; TimeSpan afterSpan = TimeSpan.Zero; int afterCount = 0;
            db.OnBeforeExecute = c => before++;
            db.OnAfterExecute = (c, ts) => { afterSpan = ts; afterCount++; };

            int result = db.ExecuteNonQuery("SELECT 1");

            result.Should().Be(42);
            before.Should().Be(1);
            afterCount.Should().Be(1);
            afterSpan.Should().BeGreaterThan(TimeSpan.Zero);
        }

        [Fact]
        public async Task ExecuteScalarAsync_Should_Return_Value_And_Hooks()
        {
            var db = new TestDbIO();
            int before = 0; int after = 0;
            db.OnBeforeExecute = _ => before++;
            db.OnAfterExecute = (_, __) => after++;
            object? val = await db.ExecuteScalarAsync("SELECT 7");
            val.Should().Be(7);
            before.Should().Be(1);
            after.Should().Be(1);
        }

        [Fact]
        public void ToDataTable_Should_Load_Data()
        {
            var db = new TestDbIO();
            var dt = db.ExecuteDataTable("SELECT Col1 FROM X");
            dt.Rows.Count.Should().Be(1);
            dt.Columns.Count.Should().Be(1);
            dt.Rows[0][0].Should().Be(99);
        }

        [Fact]
        public void Transaction_Begin_Commit_Should_Set_InTransaction_Flags()
        {
            var db = new TestDbIO();
            db.InTransaction.Should().BeFalse();
            db.BeginTransaction();
            db.InTransaction.Should().BeTrue();
            int n = db.ExecuteNonQuery("UPDATE X");
            n.Should().Be(42);
            db.CommitTransaction();
            db.InTransaction.Should().BeFalse();
        }

        [Fact]
        public void Transaction_Rollback_Should_Clear_State()
        {
            var db = new TestDbIO();
            db.BeginTransaction();
            db.ExecuteNonQuery("UPDATE Y");
            db.RollbackTransaction();
            db.InTransaction.Should().BeFalse();
        }

        [Fact]
        public void Execute_Should_Wrap_Exception_With_Custom_Code()
        {
            var db = new TestDbIO();
            Action act = () => db.ExecuteNonQuery("FAIL NOW");
            var ex = act.Should().Throw<Exception>().Which;
            ex.Message.Should().Contain("ToNonQueryFailed");
            ex.Message.Should().Contain("FAIL NOW");
        }

        [Fact]
        public void Dispose_Should_Close_Connection()
        {
            var db = new TestDbIO();
            db.ExecuteNonQuery("SELECT 1");
            var connField = typeof(DbIO).GetField("_dbConnection", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var conn = (DbConnection)connField.GetValue(db)!;
            conn.State.Should().Be(ConnectionState.Open);
            db.Dispose();
            conn.State.Should().Be(ConnectionState.Closed);
        }
    }
}
