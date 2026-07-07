// TR: LLD-0019 | ORIGIN: MID_LEVEL_DESIGN-0019
namespace AeonDocGen.Core.DTOs;

/// <summary>
/// Configuration settings for object storage or NAS integration.
/// Provides separate base paths for branding and document storage,
/// plus bounded retry configuration for transient storage operations.
/// </summary>
public sealed class StorageSettings
{
    /// <summary>
    /// Base path for tenant-scoped branding asset file persistence.
    /// </summary>
    public string BrandingBasePath { get; set; } = "/data/branding";

    /// <summary>
    /// Base path for tenant-scoped generated document file persistence.
    /// </summary>
    public string DocumentBasePath { get; set; } = "/data/documents";

    /// <summary>
    /// Maximum number of retry attempts for transient storage I/O failures.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Base delay in milliseconds between retry attempts. Actual delay uses exponential backoff.
    /// </summary>
    public int RetryBaseDelayMilliseconds { get; set; } = 200;
}
