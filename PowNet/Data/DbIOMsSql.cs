using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using PowNet.Common;
using PowNet.Configuration;
using PowNet.Extensions;

namespace PowNet.Data
{
    /// <summary>
    /// MsSql concrete implementation of DbIO.
    /// </summary>
    public class DbIOMsSql : DbIO
    {
        public DbIOMsSql(DatabaseConfiguration dbInfo) : base(dbInfo) { }

        public override DbConnection CreateConnection()
        {
            DbConnection dbConnection = new SqlConnection(DbConf.ConnectionString);
            dbConnection.Open();
            return dbConnection;
        }

        public override DataAdapter CreateDataAdapter(DbCommand dbCommand)
            => new SqlDataAdapter((SqlCommand)dbCommand);

        public override DbCommand CreateDbCommand(string commandText, DbConnection dbConnection, List<DbParameter>? dbParameters = null)
        {
            List<string> paramsInSql = commandText.ExtractSqlParameters();
            List<string> notExistParams = paramsInSql.Where(i => dbParameters?.FirstOrDefault(p => p.ParameterName.EqualsIgnoreCase(i)) == null).ToList();
            if (notExistParams.Count > 0)
            {
                dbParameters ??= [];
                foreach (string p in notExistParams)
                {
                    if (!p.EqualsIgnoreCase("InsertedTable") && !p.EqualsIgnoreCase("MasterId"))
                        dbParameters.Add(CreateParameter(p, nameof(SqlDbType.NVarChar), 4000, null));
                }
            }
            SqlCommand sqlCommand = new(commandText, (SqlConnection)dbConnection);
            if (dbParameters is not null && dbParameters.Count > 0) sqlCommand.Parameters.AddRange(dbParameters.ToArray());
            return sqlCommand;
        }

        public override DbParameter CreateParameter(string columnName, string columnType, int? columnSize = null, object? value = null)
        {
            SqlParameter op = new()
            {
                IsNullable = true,
                ParameterName = columnName,
                SqlDbType = (SqlDbType)Enum.Parse(typeof(SqlDbType), columnType, true)
            };
            if (columnSize is not null) op.Size = (int)columnSize;
            op.Value = value is null ? DBNull.Value : value;
            return op;
        }
    }
}
