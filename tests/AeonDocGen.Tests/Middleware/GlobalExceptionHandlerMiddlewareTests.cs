// TR: LLD-0029 | ORIGIN: MID_LEVEL_DESIGN-0029
using System.Text.Json;
using AeonDocGen.Api.Middleware;
using AeonDocGen.Core.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace AeonDocGen.Tests.Middleware;

public class GlobalExceptionHandlerMiddlewareTests
{
    private readonly Mock<ILogger<GlobalExceptionHandlerMiddleware>> _loggerMock;

    public GlobalExceptionHandlerMiddlewareTests()
    {
        _loggerMock = new Mock<ILogger<GlobalExceptionHandlerMiddleware>>();
    }

    private async Task<(int StatusCode, StandardErrorDto? Error)> InvokeWithException(Exception exception)
    {
        var middleware = new GlobalExceptionHandlerMiddleware(_ => throw exception, _loggerMock.Object);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var error = JsonSerializer.Deserialize<StandardErrorDto>(responseBody, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        return (context.Response.StatusCode, error);
    }

    [Fact]
    public async Task InvokeAsync_NoException_PassesThrough()
    {
        bool nextCalled = false;
        var middleware = new GlobalExceptionHandlerMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, _loggerMock.Object);

        var context = new DefaultHttpContext();
        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_ArgumentException_Returns400WithStandardError()
    {
        var (statusCode, error) = await InvokeWithException(new ArgumentException("documentType is required."));

        Assert.Equal(StatusCodes.Status400BadRequest, statusCode);
        Assert.NotNull(error);
        Assert.Equal("INVALID_REQUEST", error!.Code);
        Assert.Equal("documentType is required.", error.Message);
    }

    [Fact]
    public async Task InvokeAsync_ArgumentExceptionWithErrorCode_ExtractsCode()
    {
        var (statusCode, error) = await InvokeWithException(
            new ArgumentException("INVALID_REVIEW_ACTION:action must be one of submit, startReview, approve, reject."));

        Assert.Equal(StatusCodes.Status400BadRequest, statusCode);
        Assert.NotNull(error);
        Assert.Equal("INVALID_REVIEW_ACTION", error!.Code);
        Assert.Equal("action must be one of submit, startReview, approve, reject.", error.Message);
    }

    [Fact]
    public async Task InvokeAsync_KeyNotFoundException_Returns404()
    {
        var (statusCode, error) = await InvokeWithException(
            new KeyNotFoundException("PROJECT_NOT_FOUND:The specified project was not found."));

        Assert.Equal(StatusCodes.Status404NotFound, statusCode);
        Assert.NotNull(error);
        Assert.Equal("PROJECT_NOT_FOUND", error!.Code);
    }

    [Fact]
    public async Task InvokeAsync_KeyNotFoundException_WithoutPrefix_Returns404WithDefaultCode()
    {
        var (statusCode, error) = await InvokeWithException(
            new KeyNotFoundException("Resource not found."));

        Assert.Equal(StatusCodes.Status404NotFound, statusCode);
        Assert.NotNull(error);
        Assert.Equal("NOT_FOUND", error!.Code);
    }

    [Fact]
    public async Task InvokeAsync_UnauthorizedAccessException_Returns401()
    {
        var (statusCode, error) = await InvokeWithException(new UnauthorizedAccessException());

        Assert.Equal(StatusCodes.Status401Unauthorized, statusCode);
        Assert.NotNull(error);
        Assert.Equal("UNAUTHENTICATED", error!.Code);
    }

    [Fact]
    public async Task InvokeAsync_ETagMismatchException_Returns409()
    {
        var (statusCode, error) = await InvokeWithException(
            new InvalidOperationException("ETAG_MISMATCH:The document has been modified."));

        Assert.Equal(StatusCodes.Status409Conflict, statusCode);
        Assert.NotNull(error);
        Assert.Equal("ETAG_MISMATCH", error!.Code);
    }

    [Fact]
    public async Task InvokeAsync_IdempotencyKeyConflict_Returns409()
    {
        var (statusCode, error) = await InvokeWithException(
            new InvalidOperationException("Idempotency-Key has been used with a different payload."));

        Assert.Equal(StatusCodes.Status409Conflict, statusCode);
        Assert.NotNull(error);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED_WITH_DIFFERENT_PAYLOAD", error!.Code);
    }

    [Fact]
    public async Task InvokeAsync_MalwareDetected_Returns400()
    {
        var (statusCode, error) = await InvokeWithException(
            new InvalidOperationException("Malware scan detected unsafe content in logoFile. Upload rejected."));

        Assert.Equal(StatusCodes.Status400BadRequest, statusCode);
        Assert.NotNull(error);
        Assert.Equal("MALWARE_DETECTED", error!.Code);
    }

    [Fact]
    public async Task InvokeAsync_DocumentReviewProcessingFailed_Returns500()
    {
        var (statusCode, error) = await InvokeWithException(
            new InvalidOperationException("DOCUMENT_REVIEW_PROCESSING_FAILED:An internal error occurred."));

        Assert.Equal(StatusCodes.Status500InternalServerError, statusCode);
        Assert.NotNull(error);
        Assert.Equal("DOCUMENT_REVIEW_PROCESSING_FAILED", error!.Code);
    }

    [Fact]
    public async Task InvokeAsync_UnexpectedException_Returns500WithGenericMessage()
    {
        var (statusCode, error) = await InvokeWithException(
            new NullReferenceException("Object reference not set."));

        Assert.Equal(StatusCodes.Status500InternalServerError, statusCode);
        Assert.NotNull(error);
        Assert.Equal("INTERNAL_SERVER_ERROR", error!.Code);
        Assert.DoesNotContain("Object reference", error.Message);
    }

    [Fact]
    public async Task InvokeAsync_SetsContentTypeToJson()
    {
        var middleware = new GlobalExceptionHandlerMiddleware(
            _ => throw new Exception("test"),
            _loggerMock.Object);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal("application/json", context.Response.ContentType);
    }

    [Fact]
    public async Task InvokeAsync_LogsExceptionDetails()
    {
        var exception = new InvalidOperationException("test error");
        var middleware = new GlobalExceptionHandlerMiddleware(
            _ => throw exception,
            _loggerMock.Object);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Unhandled exception")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void MapException_PreservesTraceId()
    {
        var (_, error) = GlobalExceptionHandlerMiddleware.MapException(
            new ArgumentException("test"), "trace-123");

        Assert.Equal("trace-123", error.TraceId);
    }

    [Fact]
    public void MapException_InternalServerError_MasksDetails()
    {
        var (_, error) = GlobalExceptionHandlerMiddleware.MapException(
            new Exception("SQL deadlock on AuditLog table"), "trace-xyz");

        Assert.DoesNotContain("SQL", error.Message);
        Assert.DoesNotContain("deadlock", error.Message);
        Assert.DoesNotContain("AuditLog", error.Message);
    }
}
