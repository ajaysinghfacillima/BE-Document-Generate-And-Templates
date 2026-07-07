// TR: HLD-0004 | ORIGIN: MID_LEVEL_DESIGN-0004
namespace AeonDocGen.Core.Models;

/// <summary>
/// Represents an immutable audit log entry in the AuditLog table.
/// </summary>
public sealed class AuditLogEntity
{
    public Guid AuditLogId { get; set; }
    public Guid TenantId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int Version { get; set; }
    public Guid? ActorUserId { get; set; }
    public string ActorType { get; set; } = "user";
    public string Action { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public Guid ResourceId { get; set; }
    public string ScopeType { get; set; } = string.Empty;
    public Guid ScopeId { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public string? Reason { get; set; }
    public string ImmutableHash { get; set; } = string.Empty;
}
