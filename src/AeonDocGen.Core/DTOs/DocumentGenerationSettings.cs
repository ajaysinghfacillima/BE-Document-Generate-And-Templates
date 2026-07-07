// TR: LLD-0020 | ORIGIN: MID_LEVEL_DESIGN-0020
namespace AeonDocGen.Core.DTOs;

/// <summary>
/// Configuration settings for document generation operations.
/// </summary>
public sealed class DocumentGenerationSettings
{
    public string StorageBasePath { get; set; } = "/data/documents";
    public int MaxWatermarkLength { get; set; } = 200;
    public string FooterVersionFormat { get; set; } = "v1.0";
    public int IdempotencyRetentionHours { get; set; } = 24;
    public int RenderQueueWaitTimeoutSeconds { get; set; } = 60;
    public int RenderQueuePollIntervalMilliseconds { get; set; } = 200;
    public bool EnableInlineRenderWorkerFallback { get; set; }
    public int RenderStoreMaxRetryAttempts { get; set; } = 3;
    public int RetryBaseDelayMilliseconds { get; set; } = 500;
    public int DefaultJobTimeoutSeconds { get; set; } = 120;
    public int FailureAlertThreshold { get; set; } = 3;
    public int FailureAlertWindowMinutes { get; set; } = 15;
    public int RenderQueueBatchSize { get; set; } = 10;
}
