using AeonDocGen.Core.DTOs;
using AeonDocGen.Core.Validators;

namespace AeonDocGen.Api.Policies;

public static class RequestPolicyUtilities
{
    public static bool TryGetBearerToken(HttpContext context, out string bearerToken, out StandardErrorDto? error)
    {
        var traceId = context.TraceIdentifier;
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        var validation = HeaderValidator.ValidateAuthorizationHeader(authHeader, traceId);
        if (!validation.IsValid || string.IsNullOrWhiteSpace(validation.BearerToken) || validation.Error != null)
        {
            bearerToken = string.Empty;
            error = validation.Error;
            if (error != null)
            {
                error.Status = StatusCodes.Status401Unauthorized;
            }
            return false;
        }

        bearerToken = validation.BearerToken;
        error = null;
        return true;
    }

    public static bool TryGetTenantId(HttpContext context, out Guid tenantId, out StandardErrorDto? error)
    {
        var traceId = context.TraceIdentifier;
        tenantId = Guid.Empty;
        var tenantIdHeader = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        var validation = HeaderValidator.ValidateTenantIdHeader(tenantIdHeader, traceId);
        if (!validation.IsValid || validation.Error != null)
        {
            error = validation.Error;
            if (error != null)
            {
                error.Status = StatusCodes.Status400BadRequest;
            }
            return false;
        }

        tenantId = validation.TenantId;
        error = null;
        return true;
    }

    public static string GetCorrelationId(HttpContext context)
    {
        return HeaderValidator.ResolveCorrelationId(
            context.Request.Headers["X-Correlation-Id"].FirstOrDefault(),
            context.TraceIdentifier);
    }

    public static bool TryGetIdempotencyKey(HttpContext context, out string key, out StandardErrorDto? error)
    {
        var traceId = context.TraceIdentifier;
        key = context.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
        var validation = HeaderValidator.ValidateIdempotencyKeyHeader(key, traceId, "INVALID_REQUEST_BODY");
        if (!validation.IsValid || validation.Error != null)
        {
            error = validation.Error;
            if (error != null)
            {
                error.Status = StatusCodes.Status400BadRequest;
                error.Message = "Idempotency-Key header is required for this action.";
            }
            return false;
        }

        error = null;
        return true;
    }

    public static bool TryGetIfMatch(HttpContext context, out string ifMatch, out StandardErrorDto? error)
    {
        var traceId = context.TraceIdentifier;
        ifMatch = context.Request.Headers["If-Match"].FirstOrDefault() ?? string.Empty;
        var validation = HeaderValidator.ValidateIfMatchHeader(ifMatch, traceId, "INVALID_REQUEST_BODY");
        if (!validation.IsValid || validation.Error != null)
        {
            error = validation.Error;
            if (error != null)
            {
                error.Status = StatusCodes.Status400BadRequest;
            }
            return false;
        }

        error = null;
        return true;
    }

    public static string GetIdempotencyKeyOrGenerate(HttpContext context)
    {
        var key = context.Request.Headers["Idempotency-Key"].FirstOrDefault();
        return string.IsNullOrWhiteSpace(key) ? Guid.NewGuid().ToString() : key;
    }
}
