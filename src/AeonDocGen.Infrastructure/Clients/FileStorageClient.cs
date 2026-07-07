// TR: HLD-0019 | ORIGIN: MID_LEVEL_DESIGN-0019
using AeonDocGen.Core.DTOs;
using AeonDocGen.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AeonDocGen.Infrastructure.Clients;

/// <summary>
/// NAS/filesystem-backed durable file storage client.
/// Stores files under a configurable base path using tenant-scoped directories.
/// </summary>
public sealed class FileStorageClient : IFileStorageClient
{
    private readonly string _basePath;
    private readonly ILogger<FileStorageClient> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public FileStorageClient(
        IOptions<BrandingUploadSettings> settings,
        ILogger<FileStorageClient> logger,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        _basePath = settings.Value.StorageBasePath;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor ?? new DefaultHttpContextAccessor();
    }

    // TR: HLD-0019 | ORIGIN: MID_LEVEL_DESIGN-0019
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
        var fullPath = ResolveSafePath(canonicalPath);
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllBytesAsync(fullPath, content, cancellationToken);

        var storageUri = $"nas://{canonicalPath}";
        _logger.LogInformation("File stored successfully. Path={FullPath}, StorageUri={StorageUri}, RequestId={RequestId}", fullPath, storageUri, GetRequestId());

        return storageUri;
    }

    // TR: HLD-0019 | ORIGIN: MID_LEVEL_DESIGN-0019
    public Task DeleteFileAsync(string storageUri, CancellationToken cancellationToken = default)
    {
        if (storageUri.StartsWith("nas://", StringComparison.OrdinalIgnoreCase))
        {
            var relativePath = CanonicalizeTenantPath(storageUri["nas://".Length..]);
            var fullPath = ResolveSafePath(relativePath);

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                _logger.LogInformation("Orphaned file deleted. Path={FullPath}, RequestId={RequestId}", fullPath, GetRequestId());
            }
        }

        return Task.CompletedTask;
    }

    private string GetRequestId()
    {
        return _httpContextAccessor.HttpContext?.Request.Headers["X-Correlation-Id"].FirstOrDefault()
            ?? _httpContextAccessor.HttpContext?.TraceIdentifier
            ?? System.Diagnostics.Activity.Current?.Id
            ?? "n/a";
    }

    private string ResolveSafePath(string canonicalRelativePath)
    {
        var baseFullPath = Path.GetFullPath(_basePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(baseFullPath, canonicalRelativePath));

        if (!fullPath.StartsWith(baseFullPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Tenant-scoped storage path escapes configured base path.", nameof(canonicalRelativePath));
        }

        return fullPath;
    }

    private static string CanonicalizeTenantPath(string tenantScopedPath)
    {
        var normalized = tenantScopedPath.Replace('\\', '/').Trim();
        if (normalized.StartsWith("/", StringComparison.Ordinal) || Path.IsPathRooted(normalized))
        {
            throw new ArgumentException("Tenant-scoped storage path must be relative.", nameof(tenantScopedPath));
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0 || segments.Any(s => s == "." || s == ".." || s.Contains(':')))
        {
            throw new ArgumentException("Tenant-scoped storage path contains invalid traversal segments.", nameof(tenantScopedPath));
        }

        return string.Join('/', segments);
    }
}
