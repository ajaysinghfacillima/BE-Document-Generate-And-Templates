// TR: LLD-0020 | ORIGIN: MID_LEVEL_DESIGN-0020
using System.Data;
using AeonDocGen.Core.Interfaces;
using AeonDocGen.Core.Models;
using Dapper;

namespace AeonDocGen.Infrastructure.Repositories;

/// <summary>
/// Dapper-based repository for DocumentArtifact persistence and query.
/// </summary>
public sealed class DocumentArtifactRepository : IDocumentArtifactRepository
{
    private const string InsertSql = @"
        INSERT INTO DocumentArtifact (
            DocumentArtifactId, TenantId, ProjectId, DocumentType, Format,
            TemplateId, TemplateVersion, BrandingApplied, WatermarkApplied,
            FooterVersionText, StorageUri, ChecksumSha256, ReviewStatus,
            ReviewedByUserId, ReviewedAt, CreatedAt, UpdatedAt, Version, Etag
        ) VALUES (
            @DocumentArtifactId, @TenantId, @ProjectId, @DocumentType, @Format,
            @TemplateId, @TemplateVersion, @BrandingApplied, @WatermarkApplied,
            @FooterVersionText, @StorageUri, @ChecksumSha256, @ReviewStatus,
            @ReviewedByUserId, @ReviewedAt, @CreatedAt, @UpdatedAt, @Version, @Etag
        )";

    private const string SelectByIdSql = @"
        SELECT DocumentArtifactId, TenantId, ProjectId, DocumentType, Format,
               TemplateId, TemplateVersion, BrandingApplied, WatermarkApplied,
               FooterVersionText, StorageUri, ChecksumSha256, ReviewStatus,
               ReviewedByUserId, ReviewedAt, CreatedAt, UpdatedAt, Version, Etag
        FROM DocumentArtifact
        WHERE DocumentArtifactId = @DocumentArtifactId
          AND ProjectId = @ProjectId
          AND TenantId = @TenantId";

    private const string UpdateReviewSql = @"
        UPDATE DocumentArtifact
        SET ReviewStatus = @ReviewStatus,
            ReviewedByUserId = @ReviewedByUserId,
            ReviewedAt = @ReviewedAt,
            UpdatedAt = @UpdatedAt,
            Version = @Version,
            Etag = @Etag
        WHERE DocumentArtifactId = @DocumentArtifactId
          AND ProjectId = @ProjectId
          AND Etag = @ExpectedEtag";

    private readonly IDbConnectionFactory _connectionFactory;

    public DocumentArtifactRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    // TR: LLD-0020 | ORIGIN: MID_LEVEL_DESIGN-0020
    public async Task CreateAsync(DocumentArtifactEntity entity, IDbConnection connection, IDbTransaction transaction, CancellationToken cancellationToken = default)
    {
        await connection.ExecuteAsync(
            new CommandDefinition(InsertSql, entity, transaction: transaction, cancellationToken: cancellationToken));
    }

    // TR: LLD-0020 | ORIGIN: MID_LEVEL_DESIGN-0020
    public async Task<DocumentArtifactEntity?> GetByIdAsync(Guid documentArtifactId, Guid projectId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<DocumentArtifactEntity>(
            new CommandDefinition(SelectByIdSql, new { DocumentArtifactId = documentArtifactId, ProjectId = projectId, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<DocumentArtifactEntity?> GetByIdAsync(
        Guid documentArtifactId,
        Guid projectId,
        Guid tenantId,
        IDbConnection connection,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        return await connection.QuerySingleOrDefaultAsync<DocumentArtifactEntity>(
            new CommandDefinition(
                SelectByIdSql,
                new { DocumentArtifactId = documentArtifactId, ProjectId = projectId, TenantId = tenantId },
                transaction: transaction,
                cancellationToken: cancellationToken));
    }

    // TR: LLD-0021 | ORIGIN: MID_LEVEL_DESIGN-0021
    public async Task<int> UpdateReviewStatusAsync(DocumentArtifactEntity entity, string expectedEtag, IDbConnection connection, IDbTransaction transaction, CancellationToken cancellationToken = default)
    {
        return await connection.ExecuteAsync(
            new CommandDefinition(UpdateReviewSql,
                new
                {
                    entity.ReviewStatus,
                    entity.ReviewedByUserId,
                    entity.ReviewedAt,
                    entity.UpdatedAt,
                    entity.Version,
                    entity.Etag,
                    entity.DocumentArtifactId,
                    entity.ProjectId,
                    ExpectedEtag = expectedEtag
                },
                transaction: transaction,
                cancellationToken: cancellationToken));
    }
}
