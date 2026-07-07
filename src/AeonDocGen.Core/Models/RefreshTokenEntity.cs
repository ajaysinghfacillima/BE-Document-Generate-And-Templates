namespace AeonDocGen.Core.Models;

public sealed class RefreshTokenEntity
{
    public string Token { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public string Role { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime IssuedAtUtc { get; set; }
    public bool IsRevoked { get; set; }
}
