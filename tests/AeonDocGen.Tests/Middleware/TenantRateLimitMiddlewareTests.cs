// TR: LLD-0029 | ORIGIN: MID_LEVEL_DESIGN-0029
using AeonDocGen.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace AeonDocGen.Tests.Middleware;

public class TenantRateLimitMiddlewareTests
{
    private readonly Mock<ILogger<TenantRateLimitMiddleware>> _loggerMock;

    public TenantRateLimitMiddlewareTests()
    {
        _loggerMock = new Mock<ILogger<TenantRateLimitMiddleware>>();
    }

    [Fact]
    public async Task InvokeAsync_WhenDisabled_PassesThrough()
    {
        bool nextCalled = false;
        var options = new TenantRateLimitOptions { Enabled = false };
        var middleware = new TenantRateLimitMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, _loggerMock.Object, options);

        var context = new DefaultHttpContext();
        context.Request.Headers["X-Tenant-Id"] = "ten-001";
        context.Request.Path = "/api/v1/admin/templates";

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_NoTenantIdHeader_PassesThrough()
    {
        bool nextCalled = false;
        var middleware = new TenantRateLimitMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, _loggerMock.Object);

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/admin/templates";

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_NonRateLimitedPath_PassesThrough()
    {
        bool nextCalled = false;
        var middleware = new TenantRateLimitMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, _loggerMock.Object);

        var context = new DefaultHttpContext();
        context.Request.Headers["X-Tenant-Id"] = "ten-001";
        context.Request.Path = "/api/v1/admin/branding/assets";

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_UnderRateLimit_PassesThrough()
    {
        var options = new TenantRateLimitOptions { MaxRequestsPerWindow = 5, WindowSeconds = 60 };
        bool nextCalled = false;
        var middleware = new TenantRateLimitMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, _loggerMock.Object, options);

        var context = new DefaultHttpContext();
        context.Request.Headers["X-Tenant-Id"] = "ten-under";
        context.Request.Path = "/api/v1/admin/templates";

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_ExceedsRateLimit_Returns429()
    {
        var options = new TenantRateLimitOptions { MaxRequestsPerWindow = 2, WindowSeconds = 60 };
        var middleware = new TenantRateLimitMiddleware(ctx =>
        {
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        }, _loggerMock.Object, options);

        // Make 2 requests (within limit)
        for (int i = 0; i < 2; i++)
        {
            var ctx = new DefaultHttpContext();
            ctx.Request.Headers["X-Tenant-Id"] = "ten-rate-limit";
            ctx.Request.Path = "/api/v1/admin/templates";
            await middleware.InvokeAsync(ctx);
        }

        // Third request should be rate-limited
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Headers["X-Tenant-Id"] = "ten-rate-limit";
        context.Request.Path = "/api/v1/admin/templates";

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status429TooManyRequests, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_RateLimitReturnsStandardError()
    {
        var options = new TenantRateLimitOptions { MaxRequestsPerWindow = 1, WindowSeconds = 60 };
        var middleware = new TenantRateLimitMiddleware(ctx =>
        {
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        }, _loggerMock.Object, options);

        var ctx1 = new DefaultHttpContext();
        ctx1.Request.Headers["X-Tenant-Id"] = "ten-error-format";
        ctx1.Request.Path = "/api/v1/admin/templates";
        await middleware.InvokeAsync(ctx1);

        var ctx2 = new DefaultHttpContext();
        ctx2.Response.Body = new MemoryStream();
        ctx2.Request.Headers["X-Tenant-Id"] = "ten-error-format";
        ctx2.Request.Path = "/api/v1/admin/templates";
        await middleware.InvokeAsync(ctx2);

        ctx2.Response.Body.Position = 0;
        var body = await new StreamReader(ctx2.Response.Body).ReadToEndAsync();
        Assert.Contains("RATE_LIMIT_EXCEEDED", body);
        Assert.Contains("Too many requests", body);
    }

    [Fact]
    public async Task InvokeAsync_DifferentTenants_HaveSeparateLimits()
    {
        var options = new TenantRateLimitOptions { MaxRequestsPerWindow = 1, WindowSeconds = 60 };
        var middleware = new TenantRateLimitMiddleware(ctx =>
        {
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        }, _loggerMock.Object, options);

        // Tenant A uses their limit
        var ctxA = new DefaultHttpContext();
        ctxA.Request.Headers["X-Tenant-Id"] = "ten-A";
        ctxA.Request.Path = "/api/v1/admin/templates";
        await middleware.InvokeAsync(ctxA);

        // Tenant B should still have their own limit
        var ctxB = new DefaultHttpContext();
        ctxB.Request.Headers["X-Tenant-Id"] = "ten-B";
        ctxB.Request.Path = "/api/v1/admin/templates";
        await middleware.InvokeAsync(ctxB);

        Assert.Equal(200, ctxB.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_TemplateListingPath_IsRateLimited()
    {
        var options = new TenantRateLimitOptions { MaxRequestsPerWindow = 1, WindowSeconds = 60 };
        var middleware = new TenantRateLimitMiddleware(ctx =>
        {
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        }, _loggerMock.Object, options);

        // First request
        var ctx1 = new DefaultHttpContext();
        ctx1.Request.Headers["X-Tenant-Id"] = "ten-path";
        ctx1.Request.Path = "/api/v1/admin/templates";
        await middleware.InvokeAsync(ctx1);
        Assert.Equal(200, ctx1.Response.StatusCode);

        // Second request should be limited
        var ctx2 = new DefaultHttpContext();
        ctx2.Response.Body = new MemoryStream();
        ctx2.Request.Headers["X-Tenant-Id"] = "ten-path";
        ctx2.Request.Path = "/api/v1/admin/templates";
        await middleware.InvokeAsync(ctx2);
        Assert.Equal(429, ctx2.Response.StatusCode);
    }
}
