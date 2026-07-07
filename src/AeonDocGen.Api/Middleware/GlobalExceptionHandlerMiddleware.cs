// TR: LLD-0029 | ORIGIN: MID_LEVEL_DESIGN-0029
using System.Diagnostics;
using System.Text.Json;
using AeonDocGen.Core.DTOs;

namespace AeonDocGen.Api.Middleware;

/// <summary>
/// Global exception handler that maps all unhandled exceptions to StandardError-compatible
/// JSON responses, preserving traceId and correlation metadata for support diagnostics.
/// Internal infrastructure details are masked from clients.
/// </summary>
public sealed class GlobalExceptionHandlerMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

    public GlobalExceptionHandlerMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    // TR: LLD-0029 | ORIGIN: MID_LEVEL_DESIGN-0029
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault() ?? traceId;

        _logger.LogError(exception,
            "Unhandled exception caught by global handler. TraceId={TraceId}, CorrelationId={CorrelationId}, ExceptionType={ExceptionType}",
            traceId, correlationId, exception.GetType().Name);

        var (statusCode, error) = MapException(exception, traceId);

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var json = JsonSerializer.Serialize(error, JsonOptions);
        await context.Response.WriteAsync(json);
    }

    // TR: LLD-0029 | ORIGIN: MID_LEVEL_DESIGN-0029
    internal static (int StatusCode, StandardErrorDto Error) MapException(Exception exception, string traceId)
    {
        return exception switch
        {
            ArgumentException argEx => (StatusCodes.Status400BadRequest, new StandardErrorDto
            {
                Status = StatusCodes.Status400BadRequest,
                TraceId = traceId,
                Code = ExtractErrorCode(argEx.Message, "INVALID_REQUEST"),
                Message = StripErrorPrefix(argEx.Message)
            }),

            KeyNotFoundException knfEx => (StatusCodes.Status404NotFound, new StandardErrorDto
            {
                Status = StatusCodes.Status404NotFound,
                TraceId = traceId,
                Code = ExtractErrorCode(knfEx.Message, "NOT_FOUND"),
                Message = StripErrorPrefix(knfEx.Message)
            }),

            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, new StandardErrorDto
            {
                Status = StatusCodes.Status401Unauthorized,
                TraceId = traceId,
                Code = "UNAUTHENTICATED",
                Message = "Authentication is required or the access token is invalid."
            }),

            InvalidOperationException ioEx when ioEx.Message.Contains("ETAG_MISMATCH") =>
                (StatusCodes.Status409Conflict, new StandardErrorDto
                {
                    Status = StatusCodes.Status409Conflict,
                    TraceId = traceId,
                    Code = "ETAG_MISMATCH",
                    Message = StripErrorPrefix(ioEx.Message)
                }),

            InvalidOperationException ioEx when ioEx.Message.Contains("Idempotency-Key") =>
                (StatusCodes.Status409Conflict, new StandardErrorDto
                {
                    Status = StatusCodes.Status409Conflict,
                    TraceId = traceId,
                    Code = "IDEMPOTENCY_KEY_REUSED_WITH_DIFFERENT_PAYLOAD",
                    Message = "The provided idempotency key was already used for a different request payload."
                }),

            InvalidOperationException ioEx when ioEx.Message.Contains("Malware") =>
                (StatusCodes.Status400BadRequest, new StandardErrorDto
                {
                    Status = StatusCodes.Status400BadRequest,
                    TraceId = traceId,
                    Code = "MALWARE_DETECTED",
                    Message = StripErrorPrefix(ioEx.Message)
                }),

            InvalidOperationException ioEx when ioEx.Message.Contains("DOCUMENT_REVIEW_PROCESSING_FAILED") =>
                (StatusCodes.Status500InternalServerError, new StandardErrorDto
                {
                    Status = StatusCodes.Status500InternalServerError,
                    TraceId = traceId,
                    Code = "DOCUMENT_REVIEW_PROCESSING_FAILED",
                    Message = "An internal error occurred while recording the document review action."
                }),

            _ => (StatusCodes.Status500InternalServerError, new StandardErrorDto
            {
                Status = StatusCodes.Status500InternalServerError,
                TraceId = traceId,
                Code = "INTERNAL_SERVER_ERROR",
                Message = "An unexpected error occurred."
            })
        };
    }

    private static string ExtractErrorCode(string message, string defaultCode)
    {
        var colonIndex = message.IndexOf(':');
        if (colonIndex > 0 && colonIndex < 60)
        {
            var candidate = message[..colonIndex];
            if (candidate == candidate.ToUpperInvariant() && candidate.Contains('_'))
            {
                return candidate;
            }
        }
        return defaultCode;
    }

    private static string StripErrorPrefix(string message)
    {
        var colonIndex = message.IndexOf(':');
        if (colonIndex > 0 && colonIndex < 60)
        {
            var candidate = message[..colonIndex];
            if (candidate == candidate.ToUpperInvariant() && candidate.Contains('_'))
            {
                return message[(colonIndex + 1)..].TrimStart();
            }
        }
        return message;
    }
}
