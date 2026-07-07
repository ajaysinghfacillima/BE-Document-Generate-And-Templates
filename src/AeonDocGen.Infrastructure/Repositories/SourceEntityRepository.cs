// TR: LLD-0020 | ORIGIN: MID_LEVEL_DESIGN-0020
using AeonDocGen.Core.Interfaces;
using Dapper;

namespace AeonDocGen.Infrastructure.Repositories;

public sealed class SourceEntityRepository : ISourceEntityRepository
{
    private const string ResolveSql = @"
        SELECT TOP 1 SourceEntityType FROM (
            SELECT 'artifact' AS SourceEntityType FROM Artifact WHERE ArtifactId = @SourceEntityId AND TenantId = @TenantId AND ProjectId = @ProjectId
            UNION ALL
            SELECT 'simulationJob' FROM SimulationJob WHERE SimulationJobId = @SourceEntityId AND TenantId = @TenantId AND ProjectId = @ProjectId
            UNION ALL
            SELECT 'scorecard' FROM Scorecard WHERE ScorecardId = @SourceEntityId AND TenantId = @TenantId AND ProjectId = @ProjectId
            UNION ALL
            SELECT 'preAssessmentRun' FROM PreAssessmentRun WHERE PreAssessmentRunId = @SourceEntityId AND TenantId = @TenantId AND ProjectId = @ProjectId
            UNION ALL
            SELECT 'auditorQuery' FROM AuditorQuery WHERE AuditorQueryId = @SourceEntityId AND TenantId = @TenantId AND ProjectId = @ProjectId
            UNION ALL
            SELECT 'recommendation' FROM Recommendation WHERE RecommendationId = @SourceEntityId AND TenantId = @TenantId AND ProjectId = @ProjectId
        ) AS Sources";

    private readonly IDbConnectionFactory _connectionFactory;

    public SourceEntityRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<string?> ResolveSourceEntityTypeAsync(Guid sourceEntityId, Guid projectId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(ResolveSql, new { SourceEntityId = sourceEntityId, ProjectId = projectId, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyDictionary<Guid, string>> ResolveSourceEntityTypesAsync(
        IReadOnlyCollection<Guid> sourceEntityIds,
        Guid projectId,
        Guid tenantId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (sourceEntityIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        // Dapper expands IN lists reliably for batched source resolution.
        var selectRows = @"
            SELECT SourceEntityId, SourceEntityType FROM (
                SELECT a.ArtifactId AS SourceEntityId, 'artifact' AS SourceEntityType FROM Artifact a
                INNER JOIN SourceEntityUserAccess s ON s.SourceEntityId = a.ArtifactId AND s.SourceEntityType = 'artifact'
                    AND s.TenantId = @TenantId AND s.ProjectId = @ProjectId AND s.UserId = @ActorUserId
                WHERE a.TenantId=@TenantId AND a.ProjectId=@ProjectId AND a.ArtifactId IN @SourceIds
                UNION ALL
                SELECT sj.SimulationJobId, 'simulationJob' FROM SimulationJob sj
                INNER JOIN SourceEntityUserAccess s ON s.SourceEntityId = sj.SimulationJobId AND s.SourceEntityType = 'simulationJob'
                    AND s.TenantId = @TenantId AND s.ProjectId = @ProjectId AND s.UserId = @ActorUserId
                WHERE sj.TenantId=@TenantId AND sj.ProjectId=@ProjectId AND sj.SimulationJobId IN @SourceIds
                UNION ALL
                SELECT sc.ScorecardId, 'scorecard' FROM Scorecard sc
                INNER JOIN SourceEntityUserAccess s ON s.SourceEntityId = sc.ScorecardId AND s.SourceEntityType = 'scorecard'
                    AND s.TenantId = @TenantId AND s.ProjectId = @ProjectId AND s.UserId = @ActorUserId
                WHERE sc.TenantId=@TenantId AND sc.ProjectId=@ProjectId AND sc.ScorecardId IN @SourceIds
                UNION ALL
                SELECT pr.PreAssessmentRunId, 'preAssessmentRun' FROM PreAssessmentRun pr
                INNER JOIN SourceEntityUserAccess s ON s.SourceEntityId = pr.PreAssessmentRunId AND s.SourceEntityType = 'preAssessmentRun'
                    AND s.TenantId = @TenantId AND s.ProjectId = @ProjectId AND s.UserId = @ActorUserId
                WHERE pr.TenantId=@TenantId AND pr.ProjectId=@ProjectId AND pr.PreAssessmentRunId IN @SourceIds
                UNION ALL
                SELECT aq.AuditorQueryId, 'auditorQuery' FROM AuditorQuery aq
                INNER JOIN SourceEntityUserAccess s ON s.SourceEntityId = aq.AuditorQueryId AND s.SourceEntityType = 'auditorQuery'
                    AND s.TenantId = @TenantId AND s.ProjectId = @ProjectId AND s.UserId = @ActorUserId
                WHERE aq.TenantId=@TenantId AND aq.ProjectId=@ProjectId AND aq.AuditorQueryId IN @SourceIds
                UNION ALL
                SELECT r.RecommendationId, 'recommendation' FROM Recommendation r
                INNER JOIN SourceEntityUserAccess s ON s.SourceEntityId = r.RecommendationId AND s.SourceEntityType = 'recommendation'
                    AND s.TenantId = @TenantId AND s.ProjectId = @ProjectId AND s.UserId = @ActorUserId
                WHERE r.TenantId=@TenantId AND r.ProjectId=@ProjectId AND r.RecommendationId IN @SourceIds
            ) z;";

        using var connection = _connectionFactory.CreateConnection();
        var results = await connection.QueryAsync<(Guid SourceEntityId, string SourceEntityType)>(
            new CommandDefinition(selectRows, new { SourceIds = sourceEntityIds, ProjectId = projectId, TenantId = tenantId, ActorUserId = actorUserId }, cancellationToken: cancellationToken));

        return results
            .GroupBy(r => r.SourceEntityId)
            .ToDictionary(g => g.Key, g => g.First().SourceEntityType);
    }
}
