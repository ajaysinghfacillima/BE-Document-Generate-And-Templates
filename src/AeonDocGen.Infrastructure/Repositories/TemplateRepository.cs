// TR: HLD-0019 | ORIGIN: MID_LEVEL_DESIGN-0019
using AeonDocGen.Core.Interfaces;
using AeonDocGen.Core.Models;
using Dapper;

namespace AeonDocGen.Infrastructure.Repositories;

public sealed class TemplateRepository : ITemplateRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public TemplateRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<TemplateEntity>> GetTemplatesByTenantIdAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT t.TemplateId, t.TenantId, t.Name, t.DocumentType, t.SupportedFormatsCsv, t.CurrentVersion,
                   t.IsActive, t.CreatedAt, t.UpdatedAt, t.Version, t.Etag
            FROM content.Template t
            WHERE t.TenantId = @TenantId
            ORDER BY t.Name ASC, t.TemplateId ASC, t.DocumentType ASC;";

        using var connection = _connectionFactory.CreateConnection();
        var templates = await connection.QueryAsync<TemplateEntity>(
            new CommandDefinition(
                sql,
                new { TenantId = tenantId },
                cancellationToken: cancellationToken));
        return templates.AsList();
    }

    public async Task<IReadOnlyList<TemplateVersionEntity>> GetTemplateVersionsByTemplateIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> templateIds,
        CancellationToken cancellationToken = default)
    {
        if (templateIds.Count == 0)
        {
            return Array.Empty<TemplateVersionEntity>();
        }

        const string sql = @"
            SELECT tv.TemplateVersionId, tv.TemplateId, tv.TemplateVersion,
                   tv.IsPublished, tv.CreatedAt
            FROM content.TemplateVersion tv
            INNER JOIN content.Template t ON t.TemplateId = tv.TemplateId
            WHERE t.TenantId = @TenantId
              AND tv.TemplateId IN @TemplateIds
            ORDER BY tv.TemplateId, tv.CreatedAt DESC";

        using var connection = _connectionFactory.CreateConnection();
        var results = await connection.QueryAsync<TemplateVersionEntity>(
            new CommandDefinition(sql, new { TenantId = tenantId, TemplateIds = templateIds }, cancellationToken: cancellationToken));
        return results.AsList();
    }
}
