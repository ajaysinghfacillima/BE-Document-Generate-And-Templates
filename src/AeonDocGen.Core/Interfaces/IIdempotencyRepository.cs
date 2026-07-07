// TR: HLD-0029 | ORIGIN: MID_LEVEL_DESIGN-0029
using System.Data;
using AeonDocGen.Core.Models;

namespace AeonDocGen.Core.Interfaces;

/// <summary>
/// Repository contract for idempotent request replay tracking.
/// </summary>
public interface IIdempotencyRepository
{
    Task<IdempotencyRecordEntity?> GetByKeyAsync(string idempotencyKey, Guid tenantId, CancellationToken cancellationToken = default);
    Task InsertAsync(IdempotencyRecordEntity record, IDbConnection connection, IDbTransaction transaction, CancellationToken cancellationToken = default);
}
