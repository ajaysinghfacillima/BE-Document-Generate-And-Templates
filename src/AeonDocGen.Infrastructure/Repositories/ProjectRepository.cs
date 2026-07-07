// TR: LLD-0020 | ORIGIN: MID_LEVEL_DESIGN-0020
using System.Data;
using AeonDocGen.Core.Interfaces;
using Dapper;

namespace AeonDocGen.Infrastructure.Repositories;

/// <summary>
/// Dapper-based repository for project scope validation.
/// </summary>
public sealed class ProjectRepository : IProjectRepository
{
    private const string ExistsSql = @"
        SELECT CASE WHEN EXISTS (
            SELECT 1 FROM project.Project WHERE ProjectId = @ProjectId AND TenantId = @TenantId
        ) THEN 1 ELSE 0 END";

    private readonly IDbConnectionFactory _connectionFactory;

    public ProjectRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    // TR: LLD-0020 | ORIGIN: MID_LEVEL_DESIGN-0020
    public async Task<bool> ExistsAsync(Guid projectId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var result = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(ExistsSql, new { ProjectId = projectId, TenantId = tenantId }, cancellationToken: cancellationToken));
        return result == 1;
    }

    public async Task<bool> ExistsAsync(
        Guid projectId,
        Guid tenantId,
        IDbConnection connection,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        var result = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                ExistsSql,
                new { ProjectId = projectId, TenantId = tenantId },
                transaction: transaction,
                cancellationToken: cancellationToken));
        return result == 1;
    }
}
