// TR: LLD-0020 | ORIGIN: MID_LEVEL_DESIGN-0020
using AeonDocGen.Core.Interfaces;
using AeonDocGen.Core.Models;
using Dapper;

namespace AeonDocGen.Infrastructure.Repositories;

/// <summary>
/// Dapper-based repository for template lookup and published version resolution.
/// </summary>
public sealed class TemplateResolutionRepository : ITemplateResolutionRepository
{
    private const string GetByIdSql = @"
        SELECT TemplateId, TenantId, Name, DocumentType, SupportedFormatsCsv, CurrentVersion, IsActive,
               CreatedAt, UpdatedAt, Version, Etag
        FROM content.Template
        WHERE TemplateId = @TemplateId AND TenantId = @TenantId";

    private const string GetLatestPublishedSql = @"
        SELECT TOP 1 TemplateVersionId, TemplateId, TemplateVersion, IsPublished, CreatedAt
        FROM content.TemplateVersion
        WHERE TemplateId = @TemplateId AND IsPublished = 1
        ORDER BY CreatedAt DESC";

    private readonly IDbConnectionFactory _connectionFactory;

    public TemplateResolutionRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    // TR: LLD-0020 | ORIGIN: MID_LEVEL_DESIGN-0020
    public async Task<TemplateEntity?> GetByIdAsync(Guid templateId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<TemplateEntity>(
            new CommandDefinition(GetByIdSql, new { TemplateId = templateId, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    // TR: LLD-0020 | ORIGIN: MID_LEVEL_DESIGN-0020
    public async Task<TemplateVersionEntity?> GetLatestPublishedVersionAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<TemplateVersionEntity>(
            new CommandDefinition(GetLatestPublishedSql, new { TemplateId = templateId }, cancellationToken: cancellationToken));
    }
}
