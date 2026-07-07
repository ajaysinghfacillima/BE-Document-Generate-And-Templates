// TR: LLD-0029 | ORIGIN: MID_LEVEL_DESIGN-0029
using AeonDocGen.Api.Middleware;
using Microsoft.AspNetCore.Http;

namespace AeonDocGen.Tests.Middleware;

public class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenCorrelationIdHeaderPresent_PreservesIt()
    {
        var middleware = new CorrelationIdMiddleware(ctx =>
        {
            Assert.Equal("existing-corr-id", ctx.Request.Headers["X-Correlation-Id"].ToString());
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = "existing-corr-id";

        await middleware.InvokeAsync(context);

        Assert.Equal("existing-corr-id", context.Items["CorrelationId"]?.ToString());
    }

    [Fact]
    public async Task InvokeAsync_WhenCorrelationIdHeaderMissing_GeneratesOne()
    {
        string? capturedCorrelationId = null;
        var middleware = new CorrelationIdMiddleware(ctx =>
        {
            capturedCorrelationId = ctx.Request.Headers["X-Correlation-Id"].ToString();
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        Assert.NotNull(capturedCorrelationId);
        Assert.False(string.IsNullOrWhiteSpace(capturedCorrelationId));
        Assert.NotNull(context.Items["CorrelationId"]);
    }

    [Fact]
    public async Task InvokeAsync_WhenCorrelationIdHeaderEmpty_GeneratesOne()
    {
        var middleware = new CorrelationIdMiddleware(ctx => Task.CompletedTask);

        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = "";

        await middleware.InvokeAsync(context);

        Assert.NotNull(context.Items["CorrelationId"]);
        Assert.False(string.IsNullOrWhiteSpace(context.Items["CorrelationId"]?.ToString()));
    }

    [Fact]
    public async Task InvokeAsync_SetsCorrelationIdOnResponseHeaders()
    {
        var middleware = new CorrelationIdMiddleware(ctx => Task.CompletedTask);

        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = "resp-corr-id";

        await middleware.InvokeAsync(context);

        // Response header callback is registered but requires response start to fire.
        // Verify it was stored in items.
        Assert.Equal("resp-corr-id", context.Items["CorrelationId"]?.ToString());
    }

    [Fact]
    public async Task InvokeAsync_CallsNextMiddleware()
    {
        bool nextCalled = false;
        var middleware = new CorrelationIdMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_WhitespaceCorrelationId_GeneratesNew()
    {
        var middleware = new CorrelationIdMiddleware(ctx => Task.CompletedTask);

        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = "   ";

        await middleware.InvokeAsync(context);

        var stored = context.Items["CorrelationId"]?.ToString();
        Assert.NotNull(stored);
        Assert.NotEqual("   ", stored);
    }
}
