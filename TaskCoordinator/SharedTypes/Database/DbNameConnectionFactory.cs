using System.Data.SqlClient;

namespace Shared.Database
{
    public class DbNameConnectionFactory : DbConnectionFactory
    {
        private string _defaultConnectionName;
        public DbNameConnectionFactory(string connectionName)
        {
            this._defaultConnectionName = connectionName;
        }

        public override string GetConnectionString(string connectionName)
        {
            return GetConnectionStringByDbName(connectionName);
        }

        public string GetConnectionStringByDbName(string dbname)
        {
            string connStr = base.GetConnectionString(this._defaultConnectionName);
            System.Data.SqlClient.SqlConnectionStringBuilder scsb = new SqlConnectionStringBuilder(connStr);
            scsb.InitialCatalog = dbname;
            connStr = scsb.ToString();
            return connStr;
        }
    }
}
