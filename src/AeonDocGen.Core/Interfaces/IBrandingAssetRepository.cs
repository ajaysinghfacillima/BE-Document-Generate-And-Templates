// TR: HLD-0019 | ORIGIN: MID_LEVEL_DESIGN-0019
using System.Data;
using AeonDocGen.Core.Models;

namespace AeonDocGen.Core.Interfaces;

/// <summary>
/// Repository contract for tenant-scoped branding asset persistence.
/// </summary>
public interface IBrandingAssetRepository
{
    Task<BrandingAssetEntity?> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task CreateAsync(BrandingAssetEntity entity, IDbConnection connection, IDbTransaction transaction, CancellationToken cancellationToken = default);
    Task UpdateAsync(BrandingAssetEntity entity, IDbConnection connection, IDbTransaction transaction, CancellationToken cancellationToken = default);
}
