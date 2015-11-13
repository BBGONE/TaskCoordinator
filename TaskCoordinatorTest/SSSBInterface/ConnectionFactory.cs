using System;
using System.Collections.Generic;
using System.Text;
using Shared.Errors;
using System.Configuration;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Threading;

namespace Database.Shared
{
    public static class ConnectionFactory
    {
        public const string CONNECTION_STRING_NAME = "DBConnectionString";
       
        public static string GetDefaultConnectionString()
        {
            ConnectionStringSettings connstrings = ConfigurationManager.ConnectionStrings[CONNECTION_STRING_NAME];
            if (connstrings == null)
            {
                throw new PPSException(string.Format("Не найдена строка соединения {0} в файле конфигурации", CONNECTION_STRING_NAME));
            }
            return connstrings.ConnectionString;
        }

        public static SqlConnection GetConnection()
        {
            SqlConnection cn= (SqlConnection)DbConnectionScope.Current.GetOpenConnection(SqlClientFactory.Instance, ConnectionFactory.GetDefaultConnectionString());
            if (cn.State == System.Data.ConnectionState.Closed)
                cn.Open();
            return cn;
        }

        public static bool IsDbConnectionOK()
        {
            try
            {
                using (DbConnectionScope connectionScope = new DbConnectionScope(DbConnectionScopeOption.Required))
                {
                    var cn = ConnectionFactory.GetConnection();
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static SqlConnection GetNewConnection()
        {
            SqlConnection cn = new SqlConnection(GetDefaultConnectionString());
            if (cn.State == System.Data.ConnectionState.Closed)
                cn.Open();
            return cn;
        }

        public static async Task<bool> CheckConnectionAsync()
        {
            try
            {
                using (var conn = await GetNewConnectionAsync())
                {
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<SqlConnection> GetNewConnectionAsync()
        {
            return await GetNewConnectionAsync(CancellationToken.None);
        }

        public static async Task<SqlConnection> GetNewConnectionAsync(CancellationToken token)
        {
            var connectionString = ConnectionFactory.GetDefaultConnectionString();
            SqlConnection cn = new SqlConnection(connectionString);
            if (cn.State == System.Data.ConnectionState.Closed)
                await cn.OpenAsync(token);
            return cn;
        }
    }
}
