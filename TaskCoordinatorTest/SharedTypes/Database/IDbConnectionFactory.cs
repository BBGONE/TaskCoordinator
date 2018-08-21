using System.Data.Common;

namespace Database.Shared
{
    public interface IDbConnectionFactory
    {
        DbConnection CreateConnection(string connectionName);
    }
}
