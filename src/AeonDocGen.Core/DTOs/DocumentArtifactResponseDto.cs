// TR: LLD-0020 | ORIGIN: MID_LEVEL_DESIGN-0020
namespace AeonDocGen.Core.DTOs;

/// <summary>
/// Response DTO for a created DocumentArtifact resource.
/// </summary>
public sealed class DocumentArtifactResponseDto
{
    public string Id { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int Version { get; set; }
    public string Etag { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string TemplateId { get; set; } = string.Empty;
    public string TemplateVersion { get; set; } = string.Empty;
    public bool BrandingApplied { get; set; }
    public bool WatermarkApplied { get; set; }
    public string FooterVersionText { get; set; } = string.Empty;
    public string StorageUri { get; set; } = string.Empty;
    public string ChecksumSha256 { get; set; } = string.Empty;
    public string ReviewStatus { get; set; } = string.Empty;
}
