using System.Data;

namespace AeonDocGen.Core.Interfaces;

/// <summary>
/// Factory for creating database connections.
/// </summary>
public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}
