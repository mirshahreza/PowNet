using Microsoft.Data.SqlClient;
using PowNet.Common;
using PowNet.Configuration;
using PowNet.Extensions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PowNet.Data
{
    /// <summary>
    /// Database command executor facade (previously DbIO). Provides CRUD helpers, execution wrappers, logging hooks, async variants, transaction helpers, and safe connection handling.
    /// </summary>
    public class DbCommandExecutor : IDisposable, IAsyncDisposable
    {
        private readonly DbConnection _dbConnection;
        private DbTransaction? _transaction;
        private bool _disposed;
        private readonly object _sync = new();

        public DatabaseConfiguration DbConf { get; init; }

        /// <summary>
        /// Optional hook before command execution.
        /// </summary>
        public Action<DbCommand>? OnBeforeExecute { get; set; }
        /// <summary>
        /// Optional hook after command execution (DbCommand, elapsed time).
        /// </summary>
        public Action<DbCommand, TimeSpan>? OnAfterExecute { get; set; }

        protected DbCommandExecutor(DatabaseConfiguration dbConf)
        {
            DbConf = dbConf;
            _dbConnection = CreateConnection(); // virtual for test substitution
            EnsureConnectionOpen();
        }

        protected void EnsureConnectionOpen()
        {
            if (_dbConnection.State == ConnectionState.Open) return;
            lock (_sync)
            {
                if (_dbConnection.State != ConnectionState.Open)
                    _dbConnection.Open();
            }
        }

        protected DbCommand PrepareCommand(string commandString, List<DbParameter>? dbParameters)
        {
            EnsureConnectionOpen();
            DbCommand command = CreateDbCommand(commandString, _dbConnection, dbParameters);
            if (_transaction != null) command.Transaction = _transaction;
            return command;
        }

        #region Factory
        public static DbCommandExecutor Instance(DatabaseConfiguration dbConf)
        {
            return new DbCommandExecutor(dbConf);
        }

        public static DbCommandExecutor Instance(string connectionName = "DefaultConnection")
            => Instance(DatabaseConfiguration.FromSettings(connectionName));
        #endregion

        #region Unified execution wrappers
        protected T Execute<T>(string commandString, List<DbParameter>? dbParameters, string errorCode, Func<DbCommand, T> exec)
        {
            try
            {
                using DbCommand command = PrepareCommand(commandString, dbParameters);
                OnBeforeExecute?.Invoke(command);
                var sw = Stopwatch.StartNew();
                T result = exec(command);
                sw.Stop();
                OnAfterExecute?.Invoke(command, sw.Elapsed);
                return result;
            }
            catch (Exception ex)
            {
                throw BuildWrappedException(ex, commandString, dbParameters, errorCode);
            }
        }

        protected async Task<T> ExecuteAsync<T>(string commandString, List<DbParameter>? dbParameters, string errorCode, Func<DbCommand, Task<T>> execAsync)
        {
            try
            {
                await Task.Yield(); // ensure async path
                using DbCommand command = PrepareCommand(commandString, dbParameters);
                OnBeforeExecute?.Invoke(command);
                var sw = Stopwatch.StartNew();
                T result = await execAsync(command).ConfigureAwait(false);
                sw.Stop();
                OnAfterExecute?.Invoke(command, sw.Elapsed);
                return result;
            }
            catch (Exception ex)
            {
                throw BuildWrappedException(ex, commandString, dbParameters, errorCode);
            }
        }
        #endregion

        #region Query Helpers (sync)
        public Dictionary<string, DataTable> ToDataSet(string commandString, List<DbParameter>? dbParameters = null, List<string>? tableNames = null)
            => Execute(commandString, dbParameters, "ToDataSetFailed", command =>
            {
                using DataSet ds = new();
                var adapter = CreateDataAdapter(command);
                adapter.Fill(ds);
                var dic = new Dictionary<string, DataTable>(ds.Tables.Count);
                for (int ind = 0; ind < ds.Tables.Count; ind++)
                {
                    DataTable dt = ds.Tables[ind];
                    string tableName = (tableNames is not null && ind < tableNames.Count) ? tableNames[ind] : $"T{ind}";
                    dic.Add(tableName, dt);
                }
                return dic;
            });

        public DataTable ToDataTable(string commandString, List<DbParameter>? dbParameters = null)
            => ToDataTables(commandString, dbParameters: dbParameters, tableName: "MainDT")["MainDT"];

        public Dictionary<string, DataTable> ToDataTables(string commandString, List<DbParameter>? dbParameters = null, string? tableName = null)
            => Execute(commandString, dbParameters, "ToDataTablesFailed", command =>
            {
                using DbDataReader sdr = command.ExecuteReader();
                DataTable dt = new();
                dt.Load(sdr);
                return new Dictionary<string, DataTable> { { tableName ?? "Master", dt } };
            });

        public object? ToScalar(string commandString, List<DbParameter>? dbParameters = null)
            => Execute(commandString, dbParameters, "ToScalarFailed", command => command.ExecuteScalar());

        public int ToNonQuery(string commandString, List<DbParameter>? dbParameters = null)
            => Execute(commandString, dbParameters, "ToNonQueryFailed", command => command.ExecuteNonQuery());

        // New naming aliases for clarity (Execute*)
        public object? ExecuteScalar(string commandString, List<DbParameter>? dbParameters = null) => ToScalar(commandString, dbParameters);
        public int ExecuteNonQuery(string commandString, List<DbParameter>? dbParameters = null) => ToNonQuery(commandString, dbParameters);
        public DataTable ExecuteDataTable(string commandString, List<DbParameter>? dbParameters = null) => ToDataTable(commandString, dbParameters);
        #endregion

        #region Query Helpers (async)
        public Task<int> ToNonQueryAsync(string commandString, List<DbParameter>? dbParameters = null, CancellationToken cancellationToken = default)
            => ExecuteAsync(commandString, dbParameters, "ToNonQueryAsyncFailed", async command =>
            {
                return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            });

        public Task<object?> ToScalarAsync(string commandString, List<DbParameter>? dbParameters = null, CancellationToken cancellationToken = default)
            => ExecuteAsync(commandString, dbParameters, "ToScalarAsyncFailed", async command =>
            {
                return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            });

        public Task<Dictionary<string, DataTable>> ToDataTablesAsync(string commandString, List<DbParameter>? dbParameters = null, string? tableName = null, CancellationToken cancellationToken = default)
            => ExecuteAsync(commandString, dbParameters, "ToDataTablesAsyncFailed", async command =>
            {
                using DbDataReader sdr = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                DataTable dt = new();
                dt.Load(sdr);
                return new Dictionary<string, DataTable> { { tableName ?? "Master", dt } };
            });

        public async Task<DataTable> ToDataTableAsync(string commandString, List<DbParameter>? dbParameters = null, CancellationToken cancellationToken = default)
            => (await ToDataTablesAsync(commandString, dbParameters, "MainDT", cancellationToken).ConfigureAwait(false))["MainDT"];

        public Task<Dictionary<string, DataTable>> ToDataSetAsync(string commandString, List<DbParameter>? dbParameters = null, List<string>? tableNames = null, CancellationToken cancellationToken = default)
            => ExecuteAsync(commandString, dbParameters, "ToDataSetAsyncFailed", async command =>
            {
                // DataAdapter has no true async Fill; run on thread pool
                return await Task.Run(() =>
                {
                    using DataSet ds = new();
                    var adapter = CreateDataAdapter(command);
                    adapter.Fill(ds);
                    var dic = new Dictionary<string, DataTable>(ds.Tables.Count);
                    for (int ind = 0; ind < ds.Tables.Count; ind++)
                    {
                        DataTable dt = ds.Tables[ind];
                        string tableName = (tableNames is not null && ind < tableNames.Count) ? tableNames[ind] : $"T{ind}";
                        dic.Add(tableName, dt);
                    }
                    return dic;
                }, cancellationToken).ConfigureAwait(false);
            });

        // Async naming aliases
        public Task<int> ExecuteNonQueryAsync(string commandString, List<DbParameter>? dbParameters = null, CancellationToken cancellationToken = default) => ToNonQueryAsync(commandString, dbParameters, cancellationToken);
        public Task<object?> ExecuteScalarAsync(string commandString, List<DbParameter>? dbParameters = null, CancellationToken cancellationToken = default) => ToScalarAsync(commandString, dbParameters, cancellationToken);
        public Task<DataTable> ExecuteDataTableAsync(string commandString, List<DbParameter>? dbParameters = null, CancellationToken cancellationToken = default) => ToDataTableAsync(commandString, dbParameters, cancellationToken);
        #endregion

        #region Transactions
        public bool InTransaction => _transaction != null;

        public void BeginTransaction(IsolationLevel? isolationLevel = null)
        {
            if (InTransaction)
                throw new PowNetException("TransactionAlreadyStarted", System.Reflection.MethodBase.GetCurrentMethod()).GetEx();
            EnsureConnectionOpen();
            _transaction = isolationLevel.HasValue ? _dbConnection.BeginTransaction(isolationLevel.Value) : _dbConnection.BeginTransaction();
        }

        public void CommitTransaction()
        {
            if (!InTransaction) return;
            _transaction!.Commit();
            _transaction.Dispose();
            _transaction = null;
        }

        public void RollbackTransaction()
        {
            if (!InTransaction) return;
            _transaction!.Rollback();
            _transaction.Dispose();
            _transaction = null;
        }
        #endregion

        #region Diagnostics
        public static bool TestConnection(DatabaseConfiguration dbConf)
        {
            try
            {
                using DbCommandExecutor dbIO = Instance(dbConf);
                return dbIO._dbConnection.State == ConnectionState.Open;
            }
            catch (Exception ex)
            {
                throw new PowNetException("TestConnectionFailed", System.Reflection.MethodBase.GetCurrentMethod())
                    .AddParam("Message", ex.Message)
                    .GetEx();
            }
        }

        protected static Exception BuildWrappedException(Exception ex, string commandString, List<DbParameter>? dbParameters, string code)
        {
            var serialized = SerializeParams(dbParameters);
            var contentBuilder = new StringBuilder(ex.Message.Length + commandString.Length + serialized.Length + 128);
            contentBuilder.Append(ex.Message).Append(StringExtensions.NL)
                          .Append(commandString).Append(StringExtensions.NL)
                          .Append(serialized);

            return new PowNetException(code, System.Reflection.MethodBase.GetCurrentMethod())
                .AddParam("Message", ex.Message)
                .AddParam("Query", contentBuilder.ToString())
                .GetEx();
        }

        private static string SerializeParams(List<DbParameter>? dbParameters)
        {
            if (dbParameters is null || dbParameters.Count == 0) return "[]";
            var sb = new StringBuilder();
            sb.Append('[');
            for (int i = 0; i < dbParameters.Count; i++)
            {
                var p = dbParameters[i];
                if (i > 0) sb.Append(',');
                string? valStr = p.Value?.ToString();
                if (valStr != null && valStr.Length > 256) valStr = valStr.Substring(0, 256) + "..."; // trim long values
                sb.Append('{')
                  .Append("\"Name\":\"").Append(p.ParameterName).Append("\",")
                  .Append("\"Value\":\"").Append(valStr?.Replace("\"", "'") ?? "null").Append("\"")
                  .Append('}');
            }
            sb.Append(']');
            return sb.ToString();
        }
        #endregion

        #region Virtual / Overridable Members
        public virtual DbConnection CreateConnection()
        {
            DbConnection dbConnection = new SqlConnection(DbConf.ConnectionString);
            dbConnection.Open();
            return dbConnection;
        }
        public virtual DataAdapter CreateDataAdapter(DbCommand dbCommand)
            => new SqlDataAdapter((SqlCommand)dbCommand);

        /// <summary>
        /// Generic command creation. Providers can override for advanced behaviors (e.g., auto parameter inference).
        /// </summary>
        public virtual DbCommand CreateDbCommand(string commandText, DbConnection dbConnection, List<DbParameter>? dbParameters = null)
        {
            List<string> paramsInSql = commandText.ExtractSqlParameters();
            List<string> notExistParams = [.. paramsInSql.Where(i => dbParameters?.FirstOrDefault(p => p.ParameterName.EqualsIgnoreCase(i)) == null)];
            if (notExistParams.Count > 0)
            {
                dbParameters ??= [];
                foreach (string p in notExistParams)
                    if (!p.EqualsIgnoreCase("InsertedTable") && !p.EqualsIgnoreCase("MasterId")) dbParameters.Add(CreateParameter(p, nameof(SqlDbType.NVarChar), 4000, null));
            }
            SqlCommand sqlCommand = new(commandText, (SqlConnection)dbConnection);
            if (dbParameters is not null && dbParameters.Count > 0) sqlCommand.Parameters.AddRange(dbParameters.ToArray());
            return sqlCommand;
        }

        /// <summary>
        /// Generic parameter creation using underlying provider command. Attempts to map string type to DbType.
        /// </summary>
        public virtual DbParameter CreateParameter(string columnName, string columnType, int? columnSize = null, object? value = null)
        {
            EnsureConnectionOpen();
            using var tmpCmd = _dbConnection.CreateCommand();
            var p = tmpCmd.CreateParameter();
            p.ParameterName = columnName;
            if (!string.IsNullOrWhiteSpace(columnType) && Enum.TryParse<DbType>(columnType, true, out var dbType))
            {
                p.DbType = dbType;
            }
            if (columnSize.HasValue) p.Size = columnSize.Value;
            p.Value = value ?? DBNull.Value;
            return p;

            //SqlParameter op = new() { IsNullable = true, ParameterName = columnName, SqlDbType = Enum.Parse<SqlDbType>(columnType, true) };
            //if (columnSize is not null) op.Size = (int)columnSize;
            //op.Value = value is null ? DBNull.Value : value;
            //return op;

        }
        #endregion

        #region Dispose
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                try { _transaction?.Dispose(); } catch { /* ignore */ }
                _transaction = null;
                try { _dbConnection?.Close(); } catch { /* ignore */ }
                try { _dbConnection?.Dispose(); } catch { /* ignore */ }
            }
            _disposed = true;
        }
        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            Dispose(true); // no truly async disposal needed for DbConnection normally
            await Task.CompletedTask;
        }
        ~DbCommandExecutor() => Dispose(false);
        #endregion
    }

}
