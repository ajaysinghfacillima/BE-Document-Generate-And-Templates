// TR: LLD-0020 | ORIGIN: MID_LEVEL_DESIGN-0020
namespace AeonDocGen.Core.Interfaces;

/// <summary>
/// Repository contract for resolving and validating source entity references.
/// Supports artifact, simulationJob, scorecard, preAssessmentRun, auditorQuery, and recommendation.
/// </summary>
public interface ISourceEntityRepository
{
    /// <summary>
    /// Resolves a source entity id to its type. Returns null if the id does not exist
    /// or does not belong to the specified tenant and project.
    /// </summary>
    Task<string?> ResolveSourceEntityTypeAsync(Guid sourceEntityId, Guid projectId, Guid tenantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, string>> ResolveSourceEntityTypesAsync(
        IReadOnlyCollection<Guid> sourceEntityIds,
        Guid projectId,
        Guid tenantId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);
}
