// TR: LLD-0019 | ORIGIN: MID_LEVEL_DESIGN-0019
namespace AeonDocGen.Core.Interfaces;

/// <summary>
/// Typed storage client for tenant-scoped branding asset file persistence.
/// Binds to branding-specific base path via configuration.
/// </summary>
public interface IBrandingStorageClient : IFileStorageClient
{
}
