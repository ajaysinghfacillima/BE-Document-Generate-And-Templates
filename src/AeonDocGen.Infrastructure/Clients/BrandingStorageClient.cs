// TR: LLD-0019 | ORIGIN: MID_LEVEL_DESIGN-0019
using AeonDocGen.Core.DTOs;
using AeonDocGen.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AeonDocGen.Infrastructure.Clients;

/// <summary>
/// NAS/filesystem-backed durable storage client for tenant-scoped branding asset file persistence.
/// Supports bounded retry with exponential backoff for transient I/O failures.
/// External dependency contract: Not specified by provided official documentation.
/// </summary>
public sealed class BrandingStorageClient : IBrandingStorageClient
{
    private readonly string _basePath;
    private readonly int _maxRetryAttempts;
    private readonly int _retryBaseDelayMs;
    private readonly ILogger<BrandingStorageClient> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    // TR: LLD-0019 | ORIGIN: MID_LEVEL_DESIGN-0019
    public BrandingStorageClient(
        IOptions<StorageSettings> settings,
        ILogger<BrandingStorageClient> logger,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        _basePath = settings.Value.BrandingBasePath;
        _maxRetryAttempts = settings.Value.MaxRetryAttempts;
        _retryBaseDelayMs = settings.Value.RetryBaseDelayMilliseconds;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor ?? new DefaultHttpContextAccessor();
    }

    // TR: LLD-0019 | ORIGIN: MID_LEVEL_DESIGN-0019
    public async Task<string> StoreFileAsync(string tenantScopedPath, byte[] content, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantScopedPath))
        {
            throw new ArgumentException("Tenant-scoped storage path must not be empty.", nameof(tenantScopedPath));
        }

        if (content == null || content.Length == 0)
        {
            throw new ArgumentException("File content must not be null or empty.", nameof(content));
        }

        var canonicalPath = CanonicalizeTenantPath(tenantScopedPath);
        var fullPath = Path.Combine(_basePath, canonicalPath);
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _logger.LogInformation(
            "Branding storage write starting. TenantScopedPath={TenantScopedPath}, ContentLength={ContentLength}, BasePath={BasePath}, RequestId={RequestId}",
            tenantScopedPath, content.Length, _basePath, GetRequestId());

        await ExecuteWithRetryAsync(
            async ct => await File.WriteAllBytesAsync(fullPath, content, ct),
            tenantScopedPath,
            "StoreFile",
            cancellationToken);

        var storageUri = $"nas://{canonicalPath}";

        _logger.LogInformation(
            "Branding file stored successfully. TenantScopedPath={TenantScopedPath}, StorageUri={StorageUri}, RequestId={RequestId}",
            canonicalPath, storageUri, GetRequestId());

        return storageUri;
    }

    // TR: LLD-0019 | ORIGIN: MID_LEVEL_DESIGN-0019
    public async Task DeleteFileAsync(string storageUri, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storageUri))
        {
            return;
        }

        if (!storageUri.StartsWith("nas://", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Branding storage delete skipped for unrecognized URI scheme. StorageUri={StorageUri}, RequestId={RequestId}",
                storageUri, GetRequestId());
            return;
        }

        var relativePath = CanonicalizeTenantPath(storageUri["nas://".Length..]);
        var fullPath = Path.Combine(_basePath, relativePath);

        if (!File.Exists(fullPath))
        {
            _logger.LogInformation(
                "Branding file already absent. StorageUri={StorageUri}, FullPath={FullPath}, RequestId={RequestId}",
                storageUri, fullPath, GetRequestId());
            return;
        }

        await ExecuteWithRetryAsync(
            _ =>
            {
                File.Delete(fullPath);
                return Task.CompletedTask;
            },
            relativePath,
            "DeleteFile",
            cancellationToken);

        _logger.LogInformation(
            "Branding file deleted. StorageUri={StorageUri}, FullPath={FullPath}, RequestId={RequestId}",
            storageUri, fullPath, GetRequestId());
    }

    // TR: LLD-0019 | ORIGIN: MID_LEVEL_DESIGN-0019
    private async Task ExecuteWithRetryAsync(
        Func<CancellationToken, Task> operation,
        string tenantScopedPath,
        string operationName,
        CancellationToken cancellationToken)
    {
        int attempt = 0;
        while (true)
        {
            try
            {
                await operation(cancellationToken);
                return;
            }
            catch (IOException ex) when (attempt < _maxRetryAttempts)
            {
                attempt++;
                var delay = _retryBaseDelayMs * (1 << (attempt - 1));

                _logger.LogWarning(
                    ex,
                    "Transient branding storage I/O failure. Operation={OperationName}, TenantScopedPath={TenantScopedPath}, Attempt={Attempt}/{MaxAttempts}, RetryDelayMs={RetryDelayMs}, RequestId={RequestId}",
                    operationName, tenantScopedPath, attempt, _maxRetryAttempts, delay, GetRequestId());

                await Task.Delay(delay, cancellationToken);
            }
            catch (IOException ex)
            {
                _logger.LogError(
                    ex,
                    "Branding storage operation failed after all retry attempts. Operation={OperationName}, TenantScopedPath={TenantScopedPath}, Attempts={Attempts}, RequestId={RequestId}",
                    operationName, tenantScopedPath, attempt + 1, GetRequestId());
                throw;
            }
        }
    }

    private string GetRequestId()
    {
        return _httpContextAccessor.HttpContext?.Request.Headers["X-Correlation-Id"].FirstOrDefault()
            ?? _httpContextAccessor.HttpContext?.TraceIdentifier
            ?? System.Diagnostics.Activity.Current?.Id
            ?? "n/a";
    }

    private static string CanonicalizeTenantPath(string tenantScopedPath)
    {
        var cleaned = tenantScopedPath.Replace('\\', '/').TrimStart('/');
        var segments = cleaned.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0 || segments.Any(s => s == "." || s == ".."))
        {
            throw new ArgumentException("Tenant-scoped storage path contains invalid traversal segments.", nameof(tenantScopedPath));
        }

        return string.Join('/', segments);
    }
}
