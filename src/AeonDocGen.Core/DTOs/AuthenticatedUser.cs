// TR: HLD-0004 | ORIGIN: MID_LEVEL_DESIGN-0004
namespace AeonDocGen.Core.DTOs;

/// <summary>
/// Represents the authenticated caller extracted from the bearer token.
/// </summary>
public sealed class AuthenticatedUser
{
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public string Role { get; set; } = string.Empty;
    public HashSet<string> Permissions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<Guid> ProjectScopeIds { get; set; } = new();

    public bool HasPermission(string permission)
    {
        return Permissions.Count == 0 || Permissions.Contains(permission) || Permissions.Contains("*");
    }

    public bool HasProjectAccess(Guid projectId)
    {
        return ProjectScopeIds.Count == 0 || ProjectScopeIds.Contains(projectId);
    }
}
