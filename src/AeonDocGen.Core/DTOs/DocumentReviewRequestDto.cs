// TR: LLD-0021 | ORIGIN: MID_LEVEL_DESIGN-0021
namespace AeonDocGen.Core.DTOs;

/// <summary>
/// Request DTO for POST /api/v1/projects/{projectId}/documents/{documentId}/review.
/// </summary>
public sealed class DocumentReviewRequestDto
{
    public string Action { get; set; } = string.Empty;
    public string? Comments { get; set; }
}
