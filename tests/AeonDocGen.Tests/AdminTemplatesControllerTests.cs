using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AeonDocGen.Api.Policies;
using AeonDocGen.Core.DTOs;
using AeonDocGen.Core.Interfaces;
using AeonDocGen.Api.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace AeonDocGen.Tests;

public class AdminTemplatesControllerTests
{
    private readonly Mock<IAdminTemplateService> _serviceMock;
    private readonly Mock<IAuthService> _authServiceMock;
    private readonly Mock<ILogger<AdminTemplatesController>> _loggerMock;
    private readonly AdminTemplatesController _controller;
    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private readonly Guid _userId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    public AdminTemplatesControllerTests()
    {
        _serviceMock = new Mock<IAdminTemplateService>();
        _authServiceMock = new Mock<IAuthService>();
        _loggerMock = new Mock<ILogger<AdminTemplatesController>>();
        _controller = new AdminTemplatesController(_serviceMock.Object, new RequestAuthorizationService(_authServiceMock.Object), _loggerMock.Object);
    }

    private void SetupHttpContext(string? authorization = null, string? tenantId = null, string? correlationId = null)
    {
        var httpContext = new DefaultHttpContext();
        if (authorization != null)
            httpContext.Request.Headers["Authorization"] = authorization;
        if (tenantId != null)
            httpContext.Request.Headers["X-Tenant-Id"] = tenantId;
        if (correlationId != null)
            httpContext.Request.Headers["X-Correlation-Id"] = correlationId;

        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    private string CreateTestJwt()
    {
        var handler = new JwtSecurityTokenHandler();
        var token = new JwtSecurityToken(
            claims: new[]
            {
                new Claim("sub", _userId.ToString()),
                new Claim("tenant_id", _tenantId.ToString()),
                new Claim("role", "Admin")
            });
        return handler.WriteToken(token);
    }

    [Fact]
    public async Task ListTemplates_Returns401_WhenNoAuthorizationHeader()
    {
        SetupHttpContext(tenantId: _tenantId.ToString());

        var result = await _controller.ListTemplates(cancellationToken: CancellationToken.None);

        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(unauthorizedResult.Value);
        Assert.Equal("UNAUTHENTICATED", error.Code);
    }

    [Fact]
    public async Task ListTemplates_Returns401_WhenInvalidBearerFormat()
    {
        SetupHttpContext(authorization: "Basic abc123", tenantId: _tenantId.ToString());

        var result = await _controller.ListTemplates(cancellationToken: CancellationToken.None);

        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(unauthorizedResult.Value);
        Assert.Equal("UNAUTHENTICATED", error.Code);
    }

    [Fact]
    public async Task ListTemplates_Returns401_WhenTokenValidationFails()
    {
        SetupHttpContext(authorization: "Bearer invalid_token", tenantId: _tenantId.ToString());
        _authServiceMock.Setup(a => a.ValidateTokenAsync("invalid_token", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuthenticatedUser?)null);

        var result = await _controller.ListTemplates(cancellationToken: CancellationToken.None);

        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(unauthorizedResult.Value);
        Assert.Equal("UNAUTHENTICATED", error.Code);
    }

    [Fact]
    public async Task ListTemplates_Returns400_WhenTenantIdHeaderMissing()
    {
        var jwt = CreateTestJwt();
        SetupHttpContext(authorization: $"Bearer {jwt}");
        _authServiceMock.Setup(a => a.ValidateTokenAsync(jwt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticatedUser { UserId = _userId, TenantId = _tenantId, Role = "Admin" });

        var result = await _controller.ListTemplates(cancellationToken: CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(badRequestResult.Value);
        Assert.Equal("INVALID_REQUEST", error.Code);
    }

    [Fact]
    public async Task ListTemplates_Returns400_WhenTenantIdHeaderInvalid()
    {
        var jwt = CreateTestJwt();
        SetupHttpContext(authorization: $"Bearer {jwt}", tenantId: "not-a-guid");
        _authServiceMock.Setup(a => a.ValidateTokenAsync(jwt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticatedUser { UserId = _userId, TenantId = _tenantId, Role = "Admin" });

        var result = await _controller.ListTemplates(cancellationToken: CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(badRequestResult.Value);
        Assert.Equal("INVALID_REQUEST", error.Code);
    }

    [Fact]
    public async Task ListTemplates_Returns403_WhenTenantMismatch()
    {
        var jwt = CreateTestJwt();
        var differentTenant = Guid.NewGuid();
        SetupHttpContext(authorization: $"Bearer {jwt}", tenantId: differentTenant.ToString());
        _authServiceMock.Setup(a => a.ValidateTokenAsync(jwt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticatedUser { UserId = _userId, TenantId = _tenantId, Role = "Admin" });

        var result = await _controller.ListTemplates(cancellationToken: CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Equal("FORBIDDEN", error.Code);
    }

    [Fact]
    public async Task ListTemplates_Returns403_WhenNotAdminRole()
    {
        var jwt = CreateTestJwt();
        SetupHttpContext(authorization: $"Bearer {jwt}", tenantId: _tenantId.ToString());
        _authServiceMock.Setup(a => a.ValidateTokenAsync(jwt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticatedUser { UserId = _userId, TenantId = _tenantId, Role = "Consultant" });

        var result = await _controller.ListTemplates(cancellationToken: CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Equal("FORBIDDEN", error.Code);
    }

    [Fact]
    public async Task ListTemplates_Returns200_WithTemplateList()
    {
        var jwt = CreateTestJwt();
        SetupHttpContext(authorization: $"Bearer {jwt}", tenantId: _tenantId.ToString(), correlationId: "corr-123");
        _authServiceMock.Setup(a => a.ValidateTokenAsync(jwt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticatedUser { UserId = _userId, TenantId = _tenantId, Role = "Admin" });

        var expectedResponse = new AdminTemplateListResponseDto
        {
            Items = new List<AdminTemplateItemDto>
            {
                new()
                {
                    Id = "tpl-001",
                    Name = "LEED Narrative",
                    CurrentVersion = "2.1",
                    Versions = new List<string> { "2.1", "2.0", "1.0" },
                    DocumentTypes = new List<string> { "narrative" }
                }
            }
        };

        _serviceMock.Setup(s => s.ListTemplatesAsync(_tenantId, _userId, "corr-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var result = await _controller.ListTemplates(cancellationToken: CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AdminTemplateListResponseDto>(okResult.Value);
        Assert.Single(response.Items);
        Assert.Equal("tpl-001", response.Items[0].Id);
    }

    [Fact]
    public async Task ListTemplates_Returns200_WithEmptyList_WhenNoTemplates()
    {
        var jwt = CreateTestJwt();
        SetupHttpContext(authorization: $"Bearer {jwt}", tenantId: _tenantId.ToString());
        _authServiceMock.Setup(a => a.ValidateTokenAsync(jwt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticatedUser { UserId = _userId, TenantId = _tenantId, Role = "Admin" });

        _serviceMock.Setup(s => s.ListTemplatesAsync(_tenantId, _userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminTemplateListResponseDto { Items = new List<AdminTemplateItemDto>() });

        var result = await _controller.ListTemplates(cancellationToken: CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AdminTemplateListResponseDto>(okResult.Value);
        Assert.Empty(response.Items);
    }

    [Fact]
    public async Task ListTemplates_Returns500_WhenServiceThrowsException()
    {
        var jwt = CreateTestJwt();
        SetupHttpContext(authorization: $"Bearer {jwt}", tenantId: _tenantId.ToString());
        _authServiceMock.Setup(a => a.ValidateTokenAsync(jwt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticatedUser { UserId = _userId, TenantId = _tenantId, Role = "Admin" });

        _serviceMock.Setup(s => s.ListTemplatesAsync(_tenantId, _userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        var result = await _controller.ListTemplates(cancellationToken: CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Equal("INTERNAL_SERVER_ERROR", error.Code);
        Assert.DoesNotContain("Database", error.Message);
    }

    [Fact]
    public async Task ListTemplates_UsesTraceIdentifier_WhenNoCorrelationId()
    {
        var jwt = CreateTestJwt();
        SetupHttpContext(authorization: $"Bearer {jwt}", tenantId: _tenantId.ToString());
        _authServiceMock.Setup(a => a.ValidateTokenAsync(jwt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticatedUser { UserId = _userId, TenantId = _tenantId, Role = "Admin" });

        _serviceMock.Setup(s => s.ListTemplatesAsync(_tenantId, _userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminTemplateListResponseDto());

        var result = await _controller.ListTemplates(cancellationToken: CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        _serviceMock.Verify(s => s.ListTemplatesAsync(_tenantId, _userId, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListTemplates_Returns403_WhenRoleIsEmptyString()
    {
        var jwt = CreateTestJwt();
        SetupHttpContext(authorization: $"Bearer {jwt}", tenantId: _tenantId.ToString());
        _authServiceMock.Setup(a => a.ValidateTokenAsync(jwt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticatedUser { UserId = _userId, TenantId = _tenantId, Role = "" });

        var result = await _controller.ListTemplates(cancellationToken: CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    
}
