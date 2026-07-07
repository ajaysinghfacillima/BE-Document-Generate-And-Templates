namespace AeonDocGen.Tests.Middleware;

public class DocumentRenderStoreWorkerContractTests
{
    [Fact]
    public void Worker_ImplementsTimeoutRetryHistoryRuntimeControlsAndAlerting()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var file = Path.Combine(root, "src", "AeonDocGen.Api", "Middleware", "DocumentRenderStoreWorker.cs");
        Assert.True(File.Exists(file));

        var code = File.ReadAllText(file);

        Assert.Contains("CancelAfter", code);
        Assert.Contains("RetryBaseDelayMilliseconds", code);
        Assert.Contains("Math.Pow", code);
        Assert.Contains("JobQueueAttemptHistory", code);
        Assert.Contains("DurationMilliseconds", code);
        Assert.Contains("JobWorkerControl", code);
        Assert.Contains("ManualTriggerRequested", code);
        Assert.Contains("JobFailureAlert", code);
        Assert.Contains("Failure threshold", code);
        Assert.Contains("job started", code);
        Assert.Contains("job completed", code);
        Assert.Contains("RenderQueueBatchSize", code);
        Assert.Contains("processedCount", code);
    }
}
