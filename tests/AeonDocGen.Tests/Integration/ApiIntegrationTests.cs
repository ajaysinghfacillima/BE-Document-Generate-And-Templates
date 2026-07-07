using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;

namespace AeonDocGen.Tests.Integration;

public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(_ =>
        {
            Environment.SetEnvironmentVariable("AEONDOCGEN_CONNECTION_STRING", "Server=localhost;Database=master;Trusted_Connection=True;TrustServerCertificate=True;");
            Environment.SetEnvironmentVariable("Jwt__SigningKey", "01234567890123456789012345678901");
            Environment.SetEnvironmentVariable("BrandingUpload__MalwareScanEndpoint", "http://localhost/mock");
        });
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task SwaggerEndpoint_ReturnsOpenApiDocument()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task AuthRefresh_MalformedJson_ReturnsDeterministic400StandardError()
    {
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh")
        {
            Content = new StringContent("{ malformed", System.Text.Encoding.UTF8, "application/json")
        };

        var response = await client.SendAsync(request);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(payload);
        Assert.Equal("INVALID_REQUEST_BODY", payload["code"]?.ToString());
    }
}
