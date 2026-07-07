// TR: HLD-0019 | ORIGIN: MID_LEVEL_DESIGN-0019
namespace AeonDocGen.Core.DTOs;

/// <summary>
/// Response DTO for branding asset upload/update operations.
/// </summary>
public sealed class BrandingAssetResponseDto
{
    public string Id { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int Version { get; set; }
    public string Etag { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
