// TR: LLD-0021 | ORIGIN: MID_LEVEL_DESIGN-0021
using AeonDocGen.Core.DTOs;

namespace AeonDocGen.Core.Interfaces;

/// <summary>
/// Service contract for document review workflow operations.
/// </summary>
public interface IDocumentReviewService
{
    Task<DocumentReviewResponseDto> ReviewDocumentAsync(
        Guid tenantId,
        Guid projectId,
        Guid documentId,
        Guid actorUserId,
        string correlationId,
        string idempotencyKey,
        string ifMatchEtag,
        DocumentReviewRequestDto request,
        CancellationToken cancellationToken = default);
}
