// TR: LLD-0029 | ORIGIN: MID_LEVEL_DESIGN-0029
using AeonDocGen.Core.DTOs;
using AeonDocGen.Core.Utilities;

namespace AeonDocGen.Core.Validators;

/// <summary>
/// Reusable validation logic for cross-cutting HTTP headers:
/// Authorization, X-Tenant-Id, X-Correlation-Id, Idempotency-Key, and If-Match.
/// Used by controllers and middleware to enforce consistent header policies.
/// </summary>
public static class HeaderValidator
{
    // TR: LLD-0004 | ORIGIN: MID_LEVEL_DESIGN-0004
    public static (bool IsValid, string? BearerToken, StandardErrorDto? Error) ValidateAuthorizationHeader(
        string? authorizationHeader, string traceId)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader) ||
            !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return (false, null, new StandardErrorDto
            {
                TraceId = traceId,
                Code = "UNAUTHENTICATED",
                Message = "Authentication is required or the access token is invalid."
            });
        }

        var token = authorizationHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            return (false, null, new StandardErrorDto
            {
                TraceId = traceId,
                Code = "UNAUTHENTICATED",
                Message = "Authentication is required or the access token is invalid."
            });
        }

        return (true, token, null);
    }

    // TR: LLD-0029 | ORIGIN: MID_LEVEL_DESIGN-0029
    public static (bool IsValid, Guid TenantId, StandardErrorDto? Error) ValidateTenantIdHeader(
        string? tenantIdHeader, string traceId)
    {
        if (!OpaqueIdentifier.TryNormalize(tenantIdHeader, "tenant", out var tenantId))
        {
            return (false, Guid.Empty, new StandardErrorDto
            {
                TraceId = traceId,
                Code = "INVALID_REQUEST",
                Message = "The request could not be processed.",
                Details = new { reason = "X-Tenant-Id header is mandatory and must resolve to an active tenant context." }
            });
        }

        return (true, tenantId, null);
    }

    // TR: LLD-0004 | ORIGIN: MID_LEVEL_DESIGN-0004
    public static StandardErrorDto? ValidateTenantIsolation(
        Guid tokenTenantId, Guid headerTenantId, string traceId, string forbiddenCode = "FORBIDDEN")
    {
        if (tokenTenantId != headerTenantId)
        {
            return new StandardErrorDto
            {
                TraceId = traceId,
                Code = forbiddenCode,
                Message = "The caller is not authorized to access this resource."
            };
        }
        return null;
    }

    // TR: LLD-0004 | ORIGIN: MID_LEVEL_DESIGN-0004
    public static StandardErrorDto? ValidateRole(
        string userRole, IEnumerable<string> allowedRoles, string traceId, string forbiddenCode = "FORBIDDEN")
    {
        foreach (var role in allowedRoles)
        {
            if (string.Equals(userRole, role, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
        }

        return new StandardErrorDto
        {
            TraceId = traceId,
            Code = forbiddenCode,
            Message = "The caller is not authorized to access this resource."
        };
    }

    // TR: LLD-0029 | ORIGIN: MID_LEVEL_DESIGN-0029
    public static (bool IsValid, StandardErrorDto? Error) ValidateIdempotencyKeyHeader(
        string? idempotencyKey, string traceId, string errorCode = "INVALID_REQUEST")
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return (false, new StandardErrorDto
            {
                TraceId = traceId,
                Code = errorCode,
                Message = "Idempotency-Key header is required for this action.",
                Details = new { reason = "Idempotency-Key header is required for this POST action." }
            });
        }

        return (true, null);
    }

    // TR: LLD-0021 | ORIGIN: MID_LEVEL_DESIGN-0021
    public static (bool IsValid, StandardErrorDto? Error) ValidateIfMatchHeader(
        string? ifMatchHeader, string traceId, string errorCode = "INVALID_REQUEST_BODY")
    {
        if (string.IsNullOrWhiteSpace(ifMatchHeader))
        {
            return (false, new StandardErrorDto
            {
                TraceId = traceId,
                Code = errorCode,
                Message = "If-Match header is required for all review actions."
            });
        }

        return (true, null);
    }

    // TR: LLD-0029 | ORIGIN: MID_LEVEL_DESIGN-0029
    public static string ResolveCorrelationId(string? headerValue, string fallback)
    {
        return string.IsNullOrWhiteSpace(headerValue) ? fallback : headerValue;
    }
}
