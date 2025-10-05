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

        public override string GetSqlTemplate(QueryType dbQueryType, bool isForSubQuery = false)
        {
            if (dbQueryType is QueryType.Create)
            {
                if (!isForSubQuery)
                    return @"\nDECLARE @InsertedTable TABLE (Id {PkTypeSize});\nDECLARE @MasterId {PkTypeSize};\nINSERT INTO [{TargetTable}] \n    ({Columns}) \n        OUTPUT INSERTED.{PkName} INTO @InsertedTable \n    VALUES \n    ({Values});\nSELECT TOP 1 @MasterId=Id FROM @InsertedTable;\n{SubQueries}\nSELECT @MasterId;";
                else
                    return @"INSERT INTO [{TargetTable}] \n    ({Columns}) \n    VALUES \n    ({Values});";
            }

            if (dbQueryType is QueryType.ReadList)
            {
                if (!isForSubQuery)
                    return @"\nSELECT \n    {Columns} \n    {Aggregations} \n    {SubQueries} \n    FROM [{TargetTable}] WITH(NOLOCK) \n    {Lefts} \n    {Where} \n    {Order} \n    {Pagination};";
                else
                    return @"\nSELECT \n    {Columns} \n    FROM [{TargetTable}] WITH(NOLOCK) \n    {Lefts} \n    {Where} \n    {Order}\n    FOR JSON PATH";
            }

            if (dbQueryType is QueryType.AggregatedReadList) return @"\nSELECT \n    {Columns} \n    {Aggregations} \n    FROM [{TargetTable}] WITH(NOLOCK) \n    {Lefts} \n    {Where} \n    {GroupBy} \n    {Order} \n    {Pagination};";

            if (dbQueryType is QueryType.ReadByKey) return @"\nSELECT \n    {Columns} \n    {SubQueries} \n    FROM {TargetTable} WITH(NOLOCK) \n    {Lefts} \n    {Where};";

            if (dbQueryType is QueryType.UpdateByKey)
            {
                if (!isForSubQuery)
                    return @"{PreQueries}\nUPDATE [{TargetTable}] SET \n    {Sets} \n    {Where};\n{SubQueries}";
                else
                    return @"UPDATE [{TargetTable}] SET \n    {Sets} \n    {Where};";
            }

            if (dbQueryType is QueryType.Delete)
                return @"DELETE [{TargetTable}] \n    {Where};";

            if (dbQueryType is QueryType.DeleteByKey)
            {
                if (!isForSubQuery)
                    return @"{SubQueries}\nDELETE [{TargetTable}] \n    {Where};";
                else
                    return @"DELETE [{TargetTable}] \n    {Where};";
            }

            if (dbQueryType is QueryType.Procedure) return @"EXEC [dbo].[{StoredProcedureName}] \n    {InputParams};";

            if (dbQueryType is QueryType.TableFunction) return @"SELECT * FROM [dbo].[{FunctionName}] \n    ({InputParams});";

            if (dbQueryType is QueryType.ScalarFunction) return @"SELECT [dbo].[{FunctionName}] \n    ({InputParams});";

            throw new PowNetException("NotImplementedYet", System.Reflection.MethodBase.GetCurrentMethod())
                .AddParam("DbQueryType", dbQueryType.ToString())
                .GetEx();
        }

        public override string GetPaginationSqlTemplate() => @"OFFSET {PageIndex} ROWS FETCH NEXT {PageSize} ROWS ONLY";
        public override string GetGroupSqlTemplate() => @"GROUP BY {Groups}";
        public override string GetOrderSqlTemplate() => @"ORDER BY {Orders}";
        public override string GetLeftJoinSqlTemplate() => @"LEFT OUTER JOIN {TargetTable} AS {TargetTableAs} WITH(NOLOCK) ON [{TargetTableAs}].[{TargetColumn}]=[{MainTable}].[{MainColumn}]";
        public override string GetTranBlock() => @"BEGIN TRAN {TranName};\n{SqlBody}\nCOMMIT TRAN {TranName};";

        public override string CompileWhereCompareClause(CompareClause wcc, string source, string columnFullName, string dbParamName, string dbType)
        {
            string prefix = (dbType.EqualsIgnoreCase(SqlDbType.NChar.ToString()) ||
                             dbType.EqualsIgnoreCase(SqlDbType.NVarChar.ToString()) ||
                             dbType.EqualsIgnoreCase(SqlDbType.NText.ToString())) ? "N" : string.Empty;

            string param = DbUtils.GenParamName(source, dbParamName, null);
            return wcc.Operator switch
            {
                CompareOperator.StartsWith => $"{columnFullName} LIKE @{param} + {prefix}'%'",
                CompareOperator.EndsWith => $"{columnFullName} LIKE {prefix}'%' + @{param}",
                CompareOperator.Contains => $"{columnFullName} LIKE {prefix}'%' + @{param} + {prefix}'%'",
                CompareOperator.Equal => $"{columnFullName} = @{param}",
                CompareOperator.NotEqual => $"{columnFullName} != @{param}",
                CompareOperator.IsNull => $"{columnFullName} IS NULL",
                CompareOperator.IsNotNull => $"{columnFullName} IS NOT NULL",
                CompareOperator.LessThan => $"{columnFullName} < @{param}",
                CompareOperator.LessThanOrEqual => $"{columnFullName} <= @{param}",
                CompareOperator.MoreThan => $"{columnFullName} > @{param}",
                CompareOperator.MoreThanOrEqual => $"{columnFullName} >= @{param}",
                CompareOperator.In => $"{columnFullName} IN (@{param})",
                CompareOperator.NotIn => $"{columnFullName} NOT IN (@{param})",
                _ => string.Empty
            };
        }

        public override string DbParamToCSharpInputParam(DbParam dbParam)
        {
            string dbType = dbParam.DbType.ToLowerInvariant();
            if (dbType.Contains("char") || dbType.Contains("text") || dbType.Contains("uniqueidentifier")) return $"string {dbParam.Name}";
            if (dbType.Contains("bigint")) return $"long {dbParam.Name}";
            if (dbType.Contains("int")) return $"int {dbParam.Name}";
            if (dbType.Contains("date")) return $"DateTime {dbParam.Name}";
            if (dbType == "bit") return $"bool {dbParam.Name}";
            if (dbType is "decimal" or "money" or "numeric" or "real") return $"decimal {dbParam.Name}";
            if (dbType == "float") return $"float {dbParam.Name}";
            if (dbType is "image" or "binary") return $"byte[] {dbParam.Name}";
            return $"string {dbParam.Name}";
        }
    }
}
