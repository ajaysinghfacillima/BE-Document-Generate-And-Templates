// TR: LLD-0029 | ORIGIN: MID_LEVEL_DESIGN-0029
using System.Diagnostics;

namespace AeonDocGen.Api.Middleware;

/// <summary>
/// Ensures every request carries a correlation identifier for distributed tracing and auditability.
/// If X-Correlation-Id is not provided by the caller, a new identifier is generated.
/// The correlation id is also set on the response headers for client-side tracing.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    // TR: LLD-0029 | ORIGIN: MID_LEVEL_DESIGN-0029
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Activity.Current?.Id ?? context.TraceIdentifier;
            context.Request.Headers[CorrelationIdHeader] = correlationId;
        }

        context.Items["CorrelationId"] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdHeader] = correlationId;
            return Task.CompletedTask;
        });

        await _next(context);
    }
}
