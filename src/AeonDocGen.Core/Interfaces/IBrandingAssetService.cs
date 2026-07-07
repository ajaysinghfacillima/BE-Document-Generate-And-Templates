// TR: HLD-0019 | ORIGIN: MID_LEVEL_DESIGN-0019
using AeonDocGen.Core.DTOs;

namespace AeonDocGen.Core.Interfaces;

/// <summary>
/// Service contract for branding asset upload and update operations.
/// </summary>
public interface IBrandingAssetService
{
    Task<BrandingAssetResponseDto> UploadBrandingAssetsAsync(
        Guid tenantId,
        Guid actorUserId,
        string correlationId,
        string idempotencyKey,
        BrandingUploadInput input,
        CancellationToken cancellationToken = default);
}
