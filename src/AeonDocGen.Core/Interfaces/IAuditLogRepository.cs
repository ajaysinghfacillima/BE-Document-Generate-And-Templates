// TR: HLD-0004 | ORIGIN: MID_LEVEL_DESIGN-0004
using System.Data;
using AeonDocGen.Core.Models;

namespace AeonDocGen.Core.Interfaces;

/// <summary>
/// Repository contract for immutable audit log persistence.
/// </summary>
public interface IAuditLogRepository
{
    Task InsertAuditLogAsync(AuditLogEntity auditLog, CancellationToken cancellationToken = default);
    Task InsertAuditLogAsync(AuditLogEntity auditLog, IDbConnection connection, IDbTransaction transaction, CancellationToken cancellationToken = default);
}
