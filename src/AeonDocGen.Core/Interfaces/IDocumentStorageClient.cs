// TR: LLD-0020 | ORIGIN: MID_LEVEL_DESIGN-0020
namespace AeonDocGen.Core.Interfaces;

/// <summary>
/// Typed storage client for tenant-scoped generated document file persistence.
/// Binds to document-specific base path via configuration.
/// </summary>
public interface IDocumentStorageClient : IFileStorageClient
{
}
