// TR: LLD-0020 | ORIGIN: MID_LEVEL_DESIGN-0020
using AeonDocGen.Core.DTOs;

namespace AeonDocGen.Core.Interfaces;

/// <summary>
/// Service contract for document generation operations.
/// </summary>
public interface IDocumentGenerationService
{
    Task<DocumentArtifactResponseDto> GenerateDocumentAsync(
        Guid tenantId,
        Guid projectId,
        Guid actorUserId,
        string correlationId,
        string idempotencyKey,
        GenerateDocumentRequestDto request,
        CancellationToken cancellationToken = default);
}
