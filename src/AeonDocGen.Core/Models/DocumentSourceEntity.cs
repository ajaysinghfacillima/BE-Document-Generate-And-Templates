// TR: LLD-0020 | ORIGIN: MID_LEVEL_DESIGN-0020
namespace AeonDocGen.Core.Models;

/// <summary>
/// Represents a source linkage record in the DocumentSource table.
/// </summary>
public sealed class DocumentSourceEntity
{
    public Guid DocumentSourceId { get; set; }
    public Guid DocumentArtifactId { get; set; }
    public string SourceEntityType { get; set; } = string.Empty;
    public Guid SourceEntityId { get; set; }
}
