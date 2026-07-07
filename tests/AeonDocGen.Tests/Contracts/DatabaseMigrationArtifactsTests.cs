using System.Text.RegularExpressions;

namespace AeonDocGen.Tests.Contracts;

public class DatabaseMigrationArtifactsTests
{
    [Fact]
    public void MigrationScripts_Exist_AsVersionedUpAndDown()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var migrationDir = Path.Combine(root, "src", "AeonDocGen.Infrastructure", "Database", "Migrations");

        Assert.True(Directory.Exists(migrationDir));
        var upScripts = Directory.GetFiles(migrationDir, "*.up.sql").OrderBy(x => x).ToList();
        var downScripts = Directory.GetFiles(migrationDir, "*.down.sql").OrderBy(x => x).ToList();

        Assert.NotEmpty(upScripts);
        Assert.Equal(upScripts.Count, downScripts.Count);
        Assert.All(upScripts, file => Assert.Matches(new Regex(@"\d{3}_.*\.up\.sql$"), Path.GetFileName(file)));
        Assert.All(downScripts, file => Assert.Matches(new Regex(@"\d{3}_.*\.down\.sql$"), Path.GetFileName(file)));
    }

    [Fact]
    public void DeploymentScripts_Exist_ForApplyAndRollback()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var deployDir = Path.Combine(root, "src", "AeonDocGen.Infrastructure", "Database", "Deployment");

        Assert.True(File.Exists(Path.Combine(deployDir, "apply-migrations.sql")));
        Assert.True(File.Exists(Path.Combine(deployDir, "rollback-migrations.sql")));
    }

    [Fact]
    public void RuntimeSchemaMigration_ContainsCoreTablesIndexesAndTokenHashing()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var migration = Path.Combine(root, "src", "AeonDocGen.Infrastructure", "Database", "Migrations", "004_runtime_tables_and_security.up.sql");
        Assert.True(File.Exists(migration));

        var sql = File.ReadAllText(migration);
        Assert.Contains("CREATE TABLE DocumentArtifact", sql);
        Assert.Contains("CREATE TABLE DocumentReviewEvent", sql);
        Assert.Contains("CREATE TABLE AuditLog", sql);
        Assert.Contains("CREATE TABLE BrandingAsset", sql);
        Assert.Contains("CREATE TABLE project.Project", sql);
        Assert.Contains("CREATE TABLE JobQueue", sql);
        Assert.Contains("ResultJson", sql);
        Assert.Contains("CREATE INDEX IX_DocumentArtifact_Project_Tenant", sql);
        Assert.Contains("CREATE INDEX IX_AuditLog_TenantId_CreatedAt", sql);
        Assert.Contains("TokenHash", sql);
    }

    [Fact]
    public void JobQueueResilienceMigration_ContainsRetryHistoryControlsAndAlerts()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var migration = Path.Combine(root, "src", "AeonDocGen.Infrastructure", "Database", "Migrations", "005_jobqueue_resilience_and_controls.up.sql");
        Assert.True(File.Exists(migration));

        var sql = File.ReadAllText(migration);
        Assert.Contains("AttemptCount", sql);
        Assert.Contains("MaxAttempts", sql);
        Assert.Contains("JobTimeoutSeconds", sql);
        Assert.Contains("StartedAt", sql);
        Assert.Contains("CompletedAt", sql);
        Assert.Contains("CREATE TABLE JobQueueAttemptHistory", sql);
        Assert.Contains("DurationMilliseconds", sql);
        Assert.Contains("CREATE TABLE JobWorkerControl", sql);
        Assert.Contains("ManualTriggerRequested", sql);
        Assert.Contains("CREATE TABLE JobFailureAlert", sql);
    }
}
