using AeonDocGen.Core.Interfaces;
using AeonDocGen.Infrastructure.Data;
using AeonDocGen.Infrastructure.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace AeonDocGen.Tests.Integration;

public class SourceEntityRepositoryIntegrationTests
{
    private static string? ResolveConnectionString()
    {
        var value = Environment.GetEnvironmentVariable("AEONDOCGEN_INTEGRATION_SQL_CONNECTION_STRING");
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static async Task EnsureSchemaAsync(string connectionString)
    {
        const string sql = """
            IF OBJECT_ID('Artifact', 'U') IS NULL
            BEGIN
                CREATE TABLE Artifact (
                    ArtifactId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                    TenantId UNIQUEIDENTIFIER NOT NULL,
                    ProjectId UNIQUEIDENTIFIER NOT NULL
                );
            END;

            IF OBJECT_ID('SimulationJob', 'U') IS NULL
            BEGIN
                CREATE TABLE SimulationJob (
                    SimulationJobId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                    TenantId UNIQUEIDENTIFIER NOT NULL,
                    ProjectId UNIQUEIDENTIFIER NOT NULL
                );
            END;

            IF OBJECT_ID('Scorecard', 'U') IS NULL
            BEGIN
                CREATE TABLE Scorecard (
                    ScorecardId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                    TenantId UNIQUEIDENTIFIER NOT NULL,
                    ProjectId UNIQUEIDENTIFIER NOT NULL
                );
            END;

            IF OBJECT_ID('PreAssessmentRun', 'U') IS NULL
            BEGIN
                CREATE TABLE PreAssessmentRun (
                    PreAssessmentRunId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                    TenantId UNIQUEIDENTIFIER NOT NULL,
                    ProjectId UNIQUEIDENTIFIER NOT NULL
                );
            END;

            IF OBJECT_ID('AuditorQuery', 'U') IS NULL
            BEGIN
                CREATE TABLE AuditorQuery (
                    AuditorQueryId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                    TenantId UNIQUEIDENTIFIER NOT NULL,
                    ProjectId UNIQUEIDENTIFIER NOT NULL
                );
            END;

            IF OBJECT_ID('Recommendation', 'U') IS NULL
            BEGIN
                CREATE TABLE Recommendation (
                    RecommendationId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                    TenantId UNIQUEIDENTIFIER NOT NULL,
                    ProjectId UNIQUEIDENTIFIER NOT NULL
                );
            END;

            IF OBJECT_ID('SourceEntityUserAccess', 'U') IS NULL
            BEGIN
                CREATE TABLE SourceEntityUserAccess (
                    SourceEntityUserAccessId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                    TenantId UNIQUEIDENTIFIER NOT NULL,
                    ProjectId UNIQUEIDENTIFIER NOT NULL,
                    UserId UNIQUEIDENTIFIER NOT NULL,
                    SourceEntityId UNIQUEIDENTIFIER NOT NULL,
                    SourceEntityType NVARCHAR(100) NOT NULL,
                    CreatedAt DATETIME2 NOT NULL
                );
            END;
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task SeedAsync(
        string connectionString,
        Guid tenantId,
        Guid projectId,
        Guid actorUserId,
        Guid artifactId,
        Guid auditorQueryId,
        Guid outOfScopeRecommendationId)
    {
        const string sql = """
            INSERT INTO Artifact (ArtifactId, TenantId, ProjectId) VALUES (@ArtifactId, @TenantId, @ProjectId);
            INSERT INTO AuditorQuery (AuditorQueryId, TenantId, ProjectId) VALUES (@AuditorQueryId, @TenantId, @ProjectId);
            INSERT INTO Recommendation (RecommendationId, TenantId, ProjectId) VALUES (@OutOfScopeRecommendationId, @OtherTenantId, @ProjectId);
            INSERT INTO SourceEntityUserAccess (SourceEntityUserAccessId, TenantId, ProjectId, UserId, SourceEntityId, SourceEntityType, CreatedAt)
            VALUES (NEWID(), @TenantId, @ProjectId, @ActorUserId, @ArtifactId, N'artifact', SYSUTCDATETIME());
            INSERT INTO SourceEntityUserAccess (SourceEntityUserAccessId, TenantId, ProjectId, UserId, SourceEntityId, SourceEntityType, CreatedAt)
            VALUES (NEWID(), @TenantId, @ProjectId, @ActorUserId, @AuditorQueryId, N'auditorQuery', SYSUTCDATETIME());
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ArtifactId", artifactId);
        command.Parameters.AddWithValue("@AuditorQueryId", auditorQueryId);
        command.Parameters.AddWithValue("@OutOfScopeRecommendationId", outOfScopeRecommendationId);
        command.Parameters.AddWithValue("@TenantId", tenantId);
        command.Parameters.AddWithValue("@ProjectId", projectId);
        command.Parameters.AddWithValue("@ActorUserId", actorUserId);
        command.Parameters.AddWithValue("@OtherTenantId", Guid.NewGuid());
        await command.ExecuteNonQueryAsync();
    }

    private static async Task CleanupAsync(
        string connectionString,
        Guid artifactId,
        Guid auditorQueryId,
        Guid outOfScopeRecommendationId)
    {
        const string sql = """
            DELETE FROM Artifact WHERE ArtifactId = @ArtifactId;
            DELETE FROM AuditorQuery WHERE AuditorQueryId = @AuditorQueryId;
            DELETE FROM Recommendation WHERE RecommendationId = @OutOfScopeRecommendationId;
            DELETE FROM SourceEntityUserAccess WHERE SourceEntityId IN (@ArtifactId, @AuditorQueryId, @OutOfScopeRecommendationId);
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ArtifactId", artifactId);
        command.Parameters.AddWithValue("@AuditorQueryId", auditorQueryId);
        command.Parameters.AddWithValue("@OutOfScopeRecommendationId", outOfScopeRecommendationId);
        await command.ExecuteNonQueryAsync();
    }

    private static ISourceEntityRepository CreateRepository(string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString
            })
            .Build();
        IDbConnectionFactory factory = new SqlConnectionFactory(configuration);
        return new SourceEntityRepository(factory);
    }

    [Fact]
    public async Task SourceEntityRepository_ResolvesSupportedTenantScopedTypes_UsingRealSqlBoundary()
    {
        var connectionString = ResolveConnectionString();
        if (connectionString == null)
        {
            return;
        }
        await EnsureSchemaAsync(connectionString);

        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var actorUserId = Guid.NewGuid();
        var unauthorizedUserId = Guid.NewGuid();
        var artifactId = Guid.NewGuid();
        var auditorQueryId = Guid.NewGuid();
        var outOfScopeRecommendationId = Guid.NewGuid();

        await SeedAsync(connectionString, tenantId, projectId, actorUserId, artifactId, auditorQueryId, outOfScopeRecommendationId);
        try
        {
            var repository = CreateRepository(connectionString);
            var sourceIds = new List<Guid> { artifactId, auditorQueryId, outOfScopeRecommendationId };

            var resolved = await repository.ResolveSourceEntityTypesAsync(sourceIds, projectId, tenantId, actorUserId, CancellationToken.None);

            Assert.Equal(2, resolved.Count);
            Assert.Equal("artifact", resolved[artifactId]);
            Assert.Equal("auditorQuery", resolved[auditorQueryId]);
            Assert.False(resolved.ContainsKey(outOfScopeRecommendationId));

            var unauthorizedResolved = await repository.ResolveSourceEntityTypesAsync(sourceIds, projectId, tenantId, unauthorizedUserId, CancellationToken.None);
            Assert.Empty(unauthorizedResolved);

            var singleResolved = await repository.ResolveSourceEntityTypeAsync(auditorQueryId, projectId, tenantId, CancellationToken.None);
            Assert.Equal("auditorQuery", singleResolved);
        }
        finally
        {
            await CleanupAsync(connectionString, artifactId, auditorQueryId, outOfScopeRecommendationId);
        }
    }
}
