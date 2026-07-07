// TR: HLD-0029 | ORIGIN: MID_LEVEL_DESIGN-0029
using System.Data;
using AeonDocGen.Core.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace AeonDocGen.Infrastructure.Data;

public sealed class SqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(IConfiguration configuration)
    {
        var configured = configuration.GetConnectionString("DefaultConnection");
        var fromEnv = Environment.GetEnvironmentVariable("AEONDOCGEN_CONNECTION_STRING");
        _connectionString = string.IsNullOrWhiteSpace(fromEnv) ? configured ?? string.Empty : fromEnv;

        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException("Connection string 'DefaultConnection' must be provided via configuration or AEONDOCGEN_CONNECTION_STRING.");
        }

        var builder = new SqlConnectionStringBuilder(_connectionString)
        {
            MaxPoolSize = 100,
            MinPoolSize = 5,
            ConnectTimeout = Math.Max(15, new SqlConnectionStringBuilder(_connectionString).ConnectTimeout)
        };
        _connectionString = builder.ToString();
    }

    public IDbConnection CreateConnection()
    {
        return new SqlConnection(_connectionString);
    }
}
