using Microsoft.Data.SqlClient;

namespace Exatech_Indotel_API.Utilities.Factories
{
    public interface IDatabaseConnectionFactory
    {
        SqlConnection GetOpenConnection();
    }
}
