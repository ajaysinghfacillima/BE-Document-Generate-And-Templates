// TR: LLD-0029 | ORIGIN: MID_LEVEL_DESIGN-0029
using AeonDocGen.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace AeonDocGen.Tests.Middleware;

public class RequestLoggingMiddlewareTests
{
    private readonly Mock<ILogger<RequestLoggingMiddleware>> _loggerMock;

    public RequestLoggingMiddlewareTests()
    {
        _loggerMock = new Mock<ILogger<RequestLoggingMiddleware>>();
    }

    [Fact]
    public async Task InvokeAsync_LogsRequestReceived()
    {
        var middleware = new RequestLoggingMiddleware(ctx =>
        {
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        }, _loggerMock.Object);

        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/v1/admin/templates";
        context.Request.Headers["X-Tenant-Id"] = "ten-001";
        context.Request.Headers["X-Correlation-Id"] = "corr-001";

        await middleware.InvokeAsync(context);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Request received")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task InvokeAsync_LogsActorUserIdWhenAuthenticated()
    {
        var middleware = new RequestLoggingMiddleware(ctx =>
        {
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        }, _loggerMock.Object);

        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/v1/admin/templates";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", "user-123")
        }, "test"));

        await middleware.InvokeAsync(context);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("ActorUserId")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task InvokeAsync_LogsSuccessCompletion_For200()
    {
        var middleware = new RequestLoggingMiddleware(ctx =>
        {
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        }, _loggerMock.Object);

        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/v1/admin/templates";

        await middleware.InvokeAsync(context);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("completed successfully")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task InvokeAsync_LogsWarning_For4xxStatus()
    {
        var middleware = new RequestLoggingMiddleware(ctx =>
        {
            ctx.Response.StatusCode = 400;
            return Task.CompletedTask;
        }, _loggerMock.Object);

        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/v1/admin/branding/assets";

        await middleware.InvokeAsync(context);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("client error")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Outcome")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task InvokeAsync_LogsError_For5xxStatus()
    {
        var middleware = new RequestLoggingMiddleware(ctx =>
        {
            ctx.Response.StatusCode = 500;
            return Task.CompletedTask;
        }, _loggerMock.Object);

        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/v1/projects/prj-001/documents";

        await middleware.InvokeAsync(context);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("server error")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task InvokeAsync_MissingTenantId_LogsUnknown()
    {
        var middleware = new RequestLoggingMiddleware(ctx =>
        {
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        }, _loggerMock.Object);

        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/v1/admin/templates";

        await middleware.InvokeAsync(context);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("unknown")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task InvokeAsync_LogsLatency()
    {
        var middleware = new RequestLoggingMiddleware(ctx =>
        {
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        }, _loggerMock.Object);

        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/test";

        await middleware.InvokeAsync(context);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("LatencyMs")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task InvokeAsync_ExceptionInPipeline_StillLogsCompletion()
    {
        var middleware = new RequestLoggingMiddleware(_ =>
            throw new InvalidOperationException("test failure"), _loggerMock.Object);

        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/test";

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));

        // Despite exception, finally block should have executed logging
        _loggerMock.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Request received")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}
