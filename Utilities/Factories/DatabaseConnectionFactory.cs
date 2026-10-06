using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace Exatech_Indotel_API.Utilities.Factories
{
    public class DatabaseConnectionFactory : IDatabaseConnectionFactory
    {
        private readonly AppOptions _appOptions;
        public DatabaseConnectionFactory(IOptions<AppOptions> options)
        {
            _appOptions = options.Value;
        }
        public SqlConnection GetOpenConnection()
        {
            return new SqlConnection(_appOptions.DatabaseConnectionString);
        }
    }
}
