// TR: LLD-0021 | ORIGIN: MID_LEVEL_DESIGN-0021
namespace AeonDocGen.Core.Models;

/// <summary>
/// Represents an immutable document review event record in the DocumentReviewEvent table.
/// </summary>
public sealed class DocumentReviewEventEntity
{
    public Guid DocumentReviewEventId { get; set; }
    public Guid DocumentArtifactId { get; set; }
    public string Action { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    public string? Comments { get; set; }
    public DateTime CreatedAt { get; set; }
}
