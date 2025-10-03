using PowNet.Common;
using PowNet.Extensions;

namespace PowNet.Configuration
{
    public class DatabaseConfiguration(string dbConfName, ServerType serverType, string connectionString)
    {
        public string Name { set; get; } = dbConfName;
        public ServerType ServerType { set; get; } = serverType;
        public string ConnectionString { set; get; } = connectionString;

        public static DatabaseConfiguration FromSettings(string connectionName = "DefaultConnection")
        {
            try
            {
                string cnnString = PowNetConfiguration.GetConnectionStringByName(connectionName);
                return new DatabaseConfiguration(connectionName, GetDatabaseType(cnnString), cnnString);
            }
            catch
            {
                throw new AppEndException("ConnectionNotFound", System.Reflection.MethodBase.GetCurrentMethod())
                    .AddParam("ConnectionName", connectionName)
                    .GetEx();
            }
        }

        public static ServerType GetDatabaseType(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString)) return ServerType.Unknown;

            if (connectionString.ContainsIgnoreCase("data source=") && (connectionString.ContainsIgnoreCase("initial catalog=") || connectionString.ContainsIgnoreCase("initial catalog=")))
            {
                return ServerType.MsSql;
            }
            else if (connectionString.ContainsIgnoreCase("server=") && connectionString.ContainsIgnoreCase("database=") && (connectionString.ContainsIgnoreCase("uid=") || connectionString.ContainsIgnoreCase("username=")))
            {
                return ServerType.MySql;
            }
            else if (connectionString.ContainsIgnoreCase("host=") && connectionString.ContainsIgnoreCase("database=") && connectionString.ContainsIgnoreCase("username="))
            {
                return ServerType.Postgres;
            }
            else if (connectionString.ContainsIgnoreCase("data source=") && (connectionString.ContainsIgnoreCase("service name=") || connectionString.ContainsIgnoreCase("sid=") || connectionString.ContainsIgnoreCase("user id=")))
            {
                return ServerType.Oracle;
            }
            else
            {
                return ServerType.Unknown;
            }
        }
    }
}