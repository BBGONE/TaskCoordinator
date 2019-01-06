using System.Data.Common;

namespace Shared.Database
{
    public interface IDbConnectionFactory
    {
        DbConnection CreateConnection(string connectionName);
    }
}
