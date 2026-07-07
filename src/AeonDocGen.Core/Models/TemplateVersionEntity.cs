// TR: HLD-0019 | ORIGIN: MID_LEVEL_DESIGN-0019
namespace AeonDocGen.Core.Models;

/// <summary>
/// Represents a row from the TemplateVersion table.
/// </summary>
public sealed class TemplateVersionEntity
{
    public Guid TemplateVersionId { get; set; }
    public Guid TemplateId { get; set; }
    public string TemplateVersion { get; set; } = string.Empty;
    public bool IsPublished { get; set; }
    public DateTime CreatedAt { get; set; }
}
