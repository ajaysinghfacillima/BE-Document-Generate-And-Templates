// TR: LLD-0021 | ORIGIN: MID_LEVEL_DESIGN-0021
namespace AeonDocGen.Core.DTOs;

/// <summary>
/// Response DTO for POST /api/v1/projects/{projectId}/documents/{documentId}/review.
/// </summary>
public sealed class DocumentReviewResponseDto
{
    public string DocumentId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string ReviewStatus { get; set; } = string.Empty;
    public DocumentReviewEventDto Event { get; set; } = new();
    public string? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string Etag { get; set; } = string.Empty;
}

/// <summary>
/// Nested event DTO for document review response.
/// </summary>
public sealed class DocumentReviewEventDto
{
    public string? ReviewEventId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string ActorUserId { get; set; } = string.Empty;
    public string? Comments { get; set; }
    public DateTime CreatedAt { get; set; }
}
