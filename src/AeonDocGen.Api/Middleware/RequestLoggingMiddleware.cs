// TR: LLD-0029 | ORIGIN: MID_LEVEL_DESIGN-0029
using System.Diagnostics;

namespace AeonDocGen.Api.Middleware;

/// <summary>
/// Structured operational logging middleware that records request receipt, actor, tenant,
/// correlation id, outcome, and latency across all scoped API endpoints.
/// </summary>
public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    // TR: LLD-0029 | ORIGIN: MID_LEVEL_DESIGN-0029
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault() ?? context.TraceIdentifier;
        var tenantId = context.Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? "unknown";
        var actorUserId = context.User?.FindFirst("sub")?.Value
            ?? context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? "anonymous";
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? "/";

        _logger.LogInformation(
            "Request received. Method={Method}, Path={Path}, TenantId={TenantId}, ActorUserId={ActorUserId}, CorrelationId={CorrelationId}",
            method, path, tenantId, actorUserId, correlationId);

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["TenantId"] = tenantId,
            ["ActorUserId"] = actorUserId,
            ["RequestPath"] = path
        });

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            var statusCode = context.Response.StatusCode;

            if (statusCode >= 500)
            {
                _logger.LogError(
                    "Request completed with server error. Method={Method}, Path={Path}, StatusCode={StatusCode}, TenantId={TenantId}, ActorUserId={ActorUserId}, CorrelationId={CorrelationId}, Outcome={Outcome}, LatencyMs={LatencyMs}",
                    method, path, statusCode, tenantId, actorUserId, correlationId, "failure", stopwatch.ElapsedMilliseconds);
            }
            else if (statusCode >= 400)
            {
                _logger.LogWarning(
                    "Request completed with client error. Method={Method}, Path={Path}, StatusCode={StatusCode}, TenantId={TenantId}, ActorUserId={ActorUserId}, CorrelationId={CorrelationId}, Outcome={Outcome}, LatencyMs={LatencyMs}",
                    method, path, statusCode, tenantId, actorUserId, correlationId, "failure", stopwatch.ElapsedMilliseconds);
            }
            else
            {
                _logger.LogInformation(
                    "Request completed successfully. Method={Method}, Path={Path}, StatusCode={StatusCode}, TenantId={TenantId}, ActorUserId={ActorUserId}, CorrelationId={CorrelationId}, Outcome={Outcome}, LatencyMs={LatencyMs}",
                    method, path, statusCode, tenantId, actorUserId, correlationId, "success", stopwatch.ElapsedMilliseconds);
            }
        }
    }
}
