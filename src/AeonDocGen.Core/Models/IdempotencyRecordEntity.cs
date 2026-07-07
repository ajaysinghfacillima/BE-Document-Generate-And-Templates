// TR: HLD-0029 | ORIGIN: MID_LEVEL_DESIGN-0029
namespace AeonDocGen.Core.Models;

/// <summary>
/// Represents a stored idempotent response for replay on duplicate requests.
/// </summary>
public sealed class IdempotencyRecordEntity
{
    public string IdempotencyKey { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public string RequestHash { get; set; } = string.Empty;
    public string ResponseJson { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public DateTime CreatedAt { get; set; }
}
