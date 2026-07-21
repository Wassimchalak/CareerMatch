using System.Data;
using Microsoft.Data.SqlClient;

namespace CareerMatch.API.Data
{
    public class DbConnectionFactory
    {
        private readonly IConfiguration _configuration;

        public DbConnectionFactory(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public IDbConnection CreateConnection()
        {
            string connectionString = _configuration.GetConnectionString("DefaultConnection")!;

            return new SqlConnection(connectionString);
        }
    }
}