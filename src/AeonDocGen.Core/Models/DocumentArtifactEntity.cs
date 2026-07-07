// TR: LLD-0020 | ORIGIN: MID_LEVEL_DESIGN-0020
namespace AeonDocGen.Core.Models;

/// <summary>
/// Represents a generated document artifact record in the DocumentArtifact table.
/// </summary>
public sealed class DocumentArtifactEntity
{
    public Guid DocumentArtifactId { get; set; }
    public Guid TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public Guid TemplateId { get; set; }
    public string TemplateVersion { get; set; } = string.Empty;
    public bool BrandingApplied { get; set; }
    public bool WatermarkApplied { get; set; }
    public string FooterVersionText { get; set; } = string.Empty;
    public string StorageUri { get; set; } = string.Empty;
    public string ChecksumSha256 { get; set; } = string.Empty;
    public string ReviewStatus { get; set; } = "draft";
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int Version { get; set; }
    public string Etag { get; set; } = string.Empty;
}
