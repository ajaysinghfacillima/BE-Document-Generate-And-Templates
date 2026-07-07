// TR: LLD-0029 | ORIGIN: MID_LEVEL_DESIGN-0029
using System.Collections.Concurrent;
using System.Text.Json;
using AeonDocGen.Core.DTOs;

namespace AeonDocGen.Api.Middleware;

/// <summary>
/// Per-tenant rate limit hook middleware that enforces platform-standard rate limiting
/// for admin template listing and other scoped API endpoints.
/// Uses a sliding-window counter per tenant with configurable limits.
/// </summary>
public sealed class TenantRateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantRateLimitMiddleware> _logger;
    private readonly TenantRateLimitOptions _options;
    private readonly ConcurrentDictionary<string, TenantRateState> _tenantCounters = new();

    public TenantRateLimitMiddleware(RequestDelegate next, ILogger<TenantRateLimitMiddleware> logger, TenantRateLimitOptions? options = null)
    {
        _next = next;
        _logger = logger;
        _options = options ?? new TenantRateLimitOptions();
    }

    // TR: LLD-0029 | ORIGIN: MID_LEVEL_DESIGN-0029
    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.Enabled)
        {
            await _next(context);
            return;
        }

        var tenantId = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value ?? string.Empty;
        var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault() ?? context.TraceIdentifier;
        if (!IsRateLimitedPath(path))
        {
            await _next(context);
            return;
        }

        var now = DateTime.UtcNow;
        var state = _tenantCounters.GetOrAdd(tenantId, _ => new TenantRateState());
        var limitExceeded = false;

        lock (state)
        {
            state.PurgeExpired(now, _options.WindowSeconds);

            if (state.RequestCount >= _options.MaxRequestsPerWindow)
            {
                limitExceeded = true;
            }
            else
            {
                state.AddRequest(now);
            }
        }

        if (limitExceeded)
        {
                _logger.LogWarning(
                    "Rate limit exceeded for tenant. TenantId={TenantId}, Path={Path}, Limit={Limit}, WindowSeconds={WindowSeconds}, CorrelationId={CorrelationId}",
                    tenantId, path, _options.MaxRequestsPerWindow, _options.WindowSeconds, correlationId);

                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.ContentType = "application/json";
                var error = new StandardErrorDto
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    TraceId = context.TraceIdentifier,
                    Code = "RATE_LIMIT_EXCEEDED",
                    Message = "Too many requests. Please retry after the rate limit window resets."
                };
                await context.Response.WriteAsync(JsonSerializer.Serialize(error, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }));
                return;
        }

        await _next(context);
    }

    private static bool IsRateLimitedPath(string path)
    {
        return path.StartsWith("/api/v1/admin/templates", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/v1/admin/branding", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/v1/projects", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/v1/auth", StringComparison.OrdinalIgnoreCase);
    }

    internal sealed class TenantRateState
    {
        private readonly List<DateTime> _timestamps = new();

        public int RequestCount => _timestamps.Count;

        public void PurgeExpired(DateTime now, int windowSeconds)
        {
            var cutoff = now.AddSeconds(-windowSeconds);
            _timestamps.RemoveAll(t => t < cutoff);
        }

        public void AddRequest(DateTime timestamp)
        {
            _timestamps.Add(timestamp);
        }
    }
}

/// <summary>
/// Configuration options for per-tenant rate limiting.
/// </summary>
public sealed class TenantRateLimitOptions
{
    public bool Enabled { get; set; } = true;
    public int MaxRequestsPerWindow { get; set; } = 100;
    public int WindowSeconds { get; set; } = 60;
}
