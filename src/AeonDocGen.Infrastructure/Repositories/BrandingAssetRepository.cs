// TR: HLD-0019 | ORIGIN: MID_LEVEL_DESIGN-0019
using System.Data;
using AeonDocGen.Core.Interfaces;
using AeonDocGen.Core.Models;
using Dapper;

namespace AeonDocGen.Infrastructure.Repositories;

/// <summary>
/// Dapper-based repository for tenant-scoped branding asset persistence.
/// </summary>
public sealed class BrandingAssetRepository : IBrandingAssetRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public BrandingAssetRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    // TR: HLD-0019 | ORIGIN: MID_LEVEL_DESIGN-0019
    public async Task<BrandingAssetEntity?> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT BrandingAssetId, TenantId, Status, LogoStorageUri, FontsStorageUri,
                   ColorsJson, CreatedAt, UpdatedAt, Version, Etag
            FROM BrandingAsset
            WHERE TenantId = @TenantId";

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<BrandingAssetEntity>(
            new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    // TR: HLD-0019 | ORIGIN: MID_LEVEL_DESIGN-0019
    public async Task CreateAsync(BrandingAssetEntity entity, IDbConnection connection, IDbTransaction transaction, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            INSERT INTO BrandingAsset (
                BrandingAssetId, TenantId, Status, LogoStorageUri, FontsStorageUri,
                ColorsJson, CreatedAt, UpdatedAt, Version, Etag
            ) VALUES (
                @BrandingAssetId, @TenantId, @Status, @LogoStorageUri, @FontsStorageUri,
                @ColorsJson, @CreatedAt, @UpdatedAt, @Version, @Etag
            )";

        await connection.ExecuteAsync(
            new CommandDefinition(sql, entity, transaction: transaction, cancellationToken: cancellationToken));
    }

    // TR: HLD-0019 | ORIGIN: MID_LEVEL_DESIGN-0019
    public async Task UpdateAsync(BrandingAssetEntity entity, IDbConnection connection, IDbTransaction transaction, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE BrandingAsset
            SET Status = @Status,
                LogoStorageUri = @LogoStorageUri,
                FontsStorageUri = @FontsStorageUri,
                ColorsJson = @ColorsJson,
                UpdatedAt = @UpdatedAt,
                Version = @Version,
                Etag = @Etag
            WHERE BrandingAssetId = @BrandingAssetId AND TenantId = @TenantId";

        await connection.ExecuteAsync(
            new CommandDefinition(sql, entity, transaction: transaction, cancellationToken: cancellationToken));
    }
}
