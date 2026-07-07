// TR: HLD-0019 | ORIGIN: MID_LEVEL_DESIGN-0019
namespace AeonDocGen.Core.Interfaces;

/// <summary>
/// Provider-agnostic abstraction for durable file storage operations.
/// Concrete implementations bind to NAS, Azure Blob, or S3 via configuration.
/// </summary>
public interface IFileStorageClient
{
    Task<string> StoreFileAsync(string tenantScopedPath, byte[] content, CancellationToken cancellationToken = default);
    Task DeleteFileAsync(string storageUri, CancellationToken cancellationToken = default);
}
