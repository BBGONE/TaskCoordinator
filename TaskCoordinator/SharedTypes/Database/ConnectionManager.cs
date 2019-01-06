using System;
using Shared.Errors;
using System.Configuration;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Threading;
using System.Transactions;

namespace Shared.Database
{
    public static class ConnectionManager
    {
        public const string CONNECTION_STRING_NAME = "PPSConnectionString";
        private static Lazy<DbNameConnectionFactory> _dbNameFactory = new Lazy<DbNameConnectionFactory>(() => new DbNameConnectionFactory(CONNECTION_STRING_NAME), true);

        public static DbNameConnectionFactory DbNameFactory
        {
            get { return ConnectionManager._dbNameFactory.Value; }
        }
       
        public static string GetPPSConnectionString()
        {
            ConnectionStringSettings connstrings = ConfigurationManager.ConnectionStrings[CONNECTION_STRING_NAME];
            if (connstrings == null)
            {
                throw new PPSException(string.Format("Не найдена строка соединения {0} в файле конфигурации", CONNECTION_STRING_NAME));
            }
            return connstrings.ConnectionString;
        }

        public static string GetORAConnectionString()
        {
            ConnectionStringSettings connstrings = ConfigurationManager.ConnectionStrings["OraConnectionString"];
            if (connstrings == null)
            {
                throw new PPSException(string.Format("Не найдена строка соединения {0} в файле конфигурации", CONNECTION_STRING_NAME));
            }
            return connstrings.ConnectionString;
        }

        public static SqlConnection GetSqlConnectionByName(string connectionName)
        {
            return DbConnectionScope.GetOpenConnection<SqlConnection>(DbConnectionFactory.Instance, connectionName);
        }

        public static async Task<SqlConnection> GetSqlConnectionByNameAsync(string connectionName)
        {
            return await DbConnectionScope.GetOpenConnectionAsync<SqlConnection>(DbConnectionFactory.Instance, connectionName).ConfigureAwait(false);
        }

        public static SqlConnection GetPPSConnection()
        {
            return GetSqlConnectionByName(CONNECTION_STRING_NAME);
        }

        public static async Task<SqlConnection> GetPPSConnectionAsync()
        {
            return await GetSqlConnectionByNameAsync(CONNECTION_STRING_NAME).ConfigureAwait(false);
        }

        public static SqlConnection GetSqlConnectionByDbName(string dbname)
        {
            return DbConnectionScope.GetOpenConnection<SqlConnection>(DbNameFactory, dbname);
        }

        public static async Task<SqlConnection> GetSqlConnectionByDbNameAsync(string dbname)
        {
            return await DbConnectionScope.GetOpenConnectionAsync<SqlConnection>(DbNameFactory, dbname).ConfigureAwait(false);
        }

        public static bool IsDbConnectionOK()
        {
            try
            {
                using (DbScope dbScope = new DbScope(TransactionScopeOption.Suppress))
                {
                    var cn = ConnectionManager.GetPPSConnection();
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static SqlConnection GetNewSqlConnectionByName(string connectionName)
        {
            SqlConnection cn = (SqlConnection)DbConnectionFactory.Instance.CreateConnection(connectionName);
            if (cn.State == System.Data.ConnectionState.Closed)
                cn.Open();
            return cn;
        }

        public static SqlConnection GetNewPPSConnection()
        {
            return GetNewSqlConnectionByName(CONNECTION_STRING_NAME);
        }

        public static async Task<bool> CheckPPSConnectionAsync()
        {
            try
            {
                using (var conn = await GetNewPPSConnectionAsync().ConfigureAwait(false))
                {
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<SqlConnection> GetNewPPSConnectionAsync()
        {
            return await GetNewPPSConnectionAsync(CancellationToken.None).ConfigureAwait(false);
        }

        public static async Task<SqlConnection> GetNewPPSConnectionAsync(CancellationToken token)
        {
            SqlConnection cn = (SqlConnection)DbConnectionFactory.Instance.CreateConnection(CONNECTION_STRING_NAME);
            if (cn.State == System.Data.ConnectionState.Closed)
                await cn.OpenAsync(token).ConfigureAwait(false);
            return cn;
        }
    }
}
