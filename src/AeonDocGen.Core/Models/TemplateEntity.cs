// TR: HLD-0019 | ORIGIN: MID_LEVEL_DESIGN-0019
namespace AeonDocGen.Core.Models;

/// <summary>
/// Represents a row from the Template table scoped to a tenant.
/// </summary>
public sealed class TemplateEntity
{
    public Guid TemplateId { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string? SupportedFormatsCsv { get; set; }
    public string CurrentVersion { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int Version { get; set; }
    public string Etag { get; set; } = string.Empty;
}
