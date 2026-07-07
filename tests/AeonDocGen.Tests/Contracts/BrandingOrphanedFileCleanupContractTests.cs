namespace AeonDocGen.Tests.Contracts;

public class BrandingOrphanedFileCleanupContractTests
{
    [Fact]
    public void BrandingAssetService_QueuesOrphanedFilesWithDurableJobQueueRecord()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var file = Path.Combine(root, "src", "AeonDocGen.Core", "Services", "BrandingAssetService.cs");
        Assert.True(File.Exists(file));

        var code = File.ReadAllText(file);
        Assert.Contains("INSERT INTO JobQueue (JobQueueId, TenantId, JobType", code);
        Assert.Contains("branding.orphaned-file-cleanup", code);
        Assert.Contains("correlationId", code);
        Assert.Contains("orphanedPaths", code);
    }
}
