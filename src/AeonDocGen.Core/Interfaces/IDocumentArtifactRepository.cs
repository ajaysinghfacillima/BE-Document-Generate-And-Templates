// TR: LLD-0020 | ORIGIN: MID_LEVEL_DESIGN-0020
using System.Data;
using AeonDocGen.Core.Models;

namespace AeonDocGen.Core.Interfaces;

/// <summary>
/// Repository contract for DocumentArtifact persistence and query operations.
/// </summary>
public interface IDocumentArtifactRepository
{
    Task CreateAsync(DocumentArtifactEntity entity, IDbConnection connection, IDbTransaction transaction, CancellationToken cancellationToken = default);
    Task<DocumentArtifactEntity?> GetByIdAsync(Guid documentArtifactId, Guid projectId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<DocumentArtifactEntity?> GetByIdAsync(Guid documentArtifactId, Guid projectId, Guid tenantId, IDbConnection connection, IDbTransaction transaction, CancellationToken cancellationToken = default);
    Task<int> UpdateReviewStatusAsync(DocumentArtifactEntity entity, string expectedEtag, IDbConnection connection, IDbTransaction transaction, CancellationToken cancellationToken = default);
}
