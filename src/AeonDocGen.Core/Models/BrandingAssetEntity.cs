// TR: HLD-0019 | ORIGIN: MID_LEVEL_DESIGN-0019
namespace AeonDocGen.Core.Models;

/// <summary>
/// Represents a tenant-scoped branding asset record in the BrandingAsset table.
/// </summary>
public sealed class BrandingAssetEntity
{
    public Guid BrandingAssetId { get; set; }
    public Guid TenantId { get; set; }
    public string Status { get; set; } = "updated";
    public string? LogoStorageUri { get; set; }
    public string? FontsStorageUri { get; set; }
    public string? ColorsJson { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int Version { get; set; }
    public string Etag { get; set; } = string.Empty;
}
