using AeonDocGen.Core.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AeonDocGen.Api.Policies;

public interface IRequestAuthorizationService
{
    Task<RequestAuthorizationResult> AuthorizeAsync(
        HttpContext context,
        string forbiddenCode,
        string forbiddenMessage,
        string? requiredRole = null,
        IEnumerable<string>? requiredAnyRoles = null,
        string? requiredPermission = null,
        Guid? requiredProjectId = null,
        CancellationToken cancellationToken = default);
}

public sealed class RequestAuthorizationResult
{
    public bool IsAuthorized { get; init; }
    public Guid TenantId { get; init; }
    public AuthenticatedUser? User { get; init; }
    public IActionResult? ErrorResult { get; init; }
}
