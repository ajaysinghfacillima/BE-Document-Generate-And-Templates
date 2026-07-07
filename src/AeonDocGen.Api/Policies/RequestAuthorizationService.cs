using System.Diagnostics;
using AeonDocGen.Core.DTOs;
using AeonDocGen.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AeonDocGen.Api.Policies;

public sealed class RequestAuthorizationService : IRequestAuthorizationService
{
    private readonly IAuthService _authService;

    public RequestAuthorizationService(IAuthService authService)
    {
        _authService = authService;
    }

    public async Task<RequestAuthorizationResult> AuthorizeAsync(
        HttpContext context,
        string forbiddenCode,
        string forbiddenMessage,
        string? requiredRole = null,
        IEnumerable<string>? requiredAnyRoles = null,
        string? requiredPermission = null,
        Guid? requiredProjectId = null,
        CancellationToken cancellationToken = default)
    {
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

        if (!RequestPolicyUtilities.TryGetBearerToken(context, out var bearerToken, out var authError))
        {
            return new RequestAuthorizationResult
            {
                ErrorResult = new UnauthorizedObjectResult(authError)
            };
        }

        var authenticatedUser = await _authService.ValidateTokenAsync(bearerToken, cancellationToken);
        if (authenticatedUser == null)
        {
            return new RequestAuthorizationResult
            {
                ErrorResult = new UnauthorizedObjectResult(new StandardErrorDto
                {
                    Status = StatusCodes.Status401Unauthorized,
                    TraceId = traceId,
                    Code = "UNAUTHENTICATED",
                    Message = "Authentication is required or the access token is invalid."
                })
            };
        }

        if (!RequestPolicyUtilities.TryGetTenantId(context, out var tenantId, out var tenantError))
        {
            return new RequestAuthorizationResult
            {
                ErrorResult = new BadRequestObjectResult(tenantError)
            };
        }

        if (authenticatedUser.TenantId != tenantId)
        {
            return Forbid(traceId, forbiddenCode, forbiddenMessage);
        }

        if (!string.IsNullOrWhiteSpace(requiredRole)
            && !string.Equals(authenticatedUser.Role, requiredRole, StringComparison.OrdinalIgnoreCase))
        {
            return Forbid(traceId, forbiddenCode, forbiddenMessage);
        }

        if (requiredAnyRoles != null)
        {
            var allowedRoles = requiredAnyRoles
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (allowedRoles.Count > 0 && !allowedRoles.Contains(authenticatedUser.Role))
            {
                return Forbid(traceId, forbiddenCode, forbiddenMessage);
            }
        }

        if (!string.IsNullOrWhiteSpace(requiredPermission)
            && (!authenticatedUser.HasPermission(requiredPermission)
                || (requiredProjectId.HasValue && !authenticatedUser.HasProjectAccess(requiredProjectId.Value))))
        {
            return Forbid(traceId, forbiddenCode, forbiddenMessage);
        }

        return new RequestAuthorizationResult
        {
            IsAuthorized = true,
            TenantId = tenantId,
            User = authenticatedUser
        };
    }

    private static RequestAuthorizationResult Forbid(string traceId, string code, string message)
    {
        return new RequestAuthorizationResult
        {
            ErrorResult = new ObjectResult(new StandardErrorDto
            {
                Status = StatusCodes.Status403Forbidden,
                TraceId = traceId,
                Code = code,
                Message = message
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            }
        };
    }
}
