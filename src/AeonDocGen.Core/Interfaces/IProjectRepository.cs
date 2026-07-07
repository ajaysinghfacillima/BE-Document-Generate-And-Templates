// TR: LLD-0020 | ORIGIN: MID_LEVEL_DESIGN-0020
using System.Data;

namespace AeonDocGen.Core.Interfaces;

/// <summary>
/// Repository contract for project scope validation.
/// </summary>
public interface IProjectRepository
{
    Task<bool> ExistsAsync(Guid projectId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid projectId, Guid tenantId, IDbConnection connection, IDbTransaction transaction, CancellationToken cancellationToken = default);
}
