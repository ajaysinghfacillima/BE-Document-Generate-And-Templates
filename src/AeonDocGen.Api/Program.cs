using AeonDocGen.Api.Extensions;
using AeonDocGen.Api.Middleware;
using AeonDocGen.Core.DTOs;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.JsonWriterOptions = new JsonWriterOptions { Indented = false };
});

builder.Services
    .AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var traceId = System.Diagnostics.Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
            return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(new StandardErrorDto
            {
                Status = StatusCodes.Status400BadRequest,
                TraceId = traceId,
                Code = "INVALID_REQUEST_BODY",
                Message = "The request payload is malformed."
            });
        };
    });
builder.Services.AddApplicationServices(builder.Configuration);

var app = builder.Build();

var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("HostLifecycle");
AppDomain.CurrentDomain.UnhandledException += (_, e) =>
{
    startupLogger.LogCritical(e.ExceptionObject as Exception, "Unhandled process exception. RequestId={RequestId}", "process");
};
TaskScheduler.UnobservedTaskException += (_, e) =>
{
    startupLogger.LogCritical(e.Exception, "Unobserved task exception. RequestId={RequestId}", "process");
    e.SetObserved();
};

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseResponseCompression();
app.UseCors("ApiCors");
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<TenantRateLimitMiddleware>();

app.UseHttpsRedirection();
app.MapControllers();
app.MapHealthChecks("/health");
app.UseSwagger();
app.UseSwaggerUI();

app.Run();

public partial class Program { }
