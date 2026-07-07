using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AeonDocGen.Core.DTOs;
using AeonDocGen.Core.Interfaces;
using AeonDocGen.Api.Controllers;
using AeonDocGen.Api.Policies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace AeonDocGen.Tests;

public class BrandingAssetsControllerTests
{
    private readonly Mock<IBrandingAssetService> _serviceMock;
    private readonly Mock<IAuthService> _authServiceMock;
    private readonly Mock<ILogger<BrandingAssetsController>> _loggerMock;
    private readonly BrandingAssetsController _controller;
    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private readonly Guid _userId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    public BrandingAssetsControllerTests()
    {
        _serviceMock = new Mock<IBrandingAssetService>();
        _authServiceMock = new Mock<IAuthService>();
        _loggerMock = new Mock<ILogger<BrandingAssetsController>>();
        _controller = new BrandingAssetsController(
            _serviceMock.Object,
            new RequestAuthorizationService(_authServiceMock.Object),
            _loggerMock.Object);
    }

    private void SetupHttpContext(string? authorization = null, string? tenantId = null, string? correlationId = null, string? idempotencyKey = null)
    {
        var httpContext = new DefaultHttpContext();
        if (authorization != null)
            httpContext.Request.Headers["Authorization"] = authorization;
        if (tenantId != null)
            httpContext.Request.Headers["X-Tenant-Id"] = tenantId;
        if (correlationId != null)
            httpContext.Request.Headers["X-Correlation-Id"] = correlationId;
        if (idempotencyKey != null)
            httpContext.Request.Headers["Idempotency-Key"] = idempotencyKey;

        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    private string CreateTestJwt(string? role = "Admin")
    {
        var handler = new JwtSecurityTokenHandler();
        var token = new JwtSecurityToken(
            claims: new[]
            {
                new Claim("sub", _userId.ToString()),
                new Claim("tenant_id", _tenantId.ToString()),
                new Claim("role", role ?? string.Empty)
            });
        return handler.WriteToken(token);
    }

    private void SetupValidAuthContext(string role = "Admin")
    {
        var jwt = CreateTestJwt(role);
        SetupHttpContext($"Bearer {jwt}", _tenantId.ToString(), "corr-1", "idem-key-1");
        _authServiceMock.Setup(a => a.ValidateTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticatedUser
            {
                UserId = _userId,
                TenantId = _tenantId,
                Role = role,
                Permissions = new HashSet<string>(new[] { "branding.settings.write" }, StringComparer.OrdinalIgnoreCase)
            });
    }

    [Fact]
    public async Task UploadBrandingAssets_MissingAuthorizationHeader_Returns401()
    {
        SetupHttpContext(tenantId: _tenantId.ToString(), idempotencyKey: "key-1");
        var result = await _controller.UploadBrandingAssets(null, null, null, CancellationToken.None);
        var objResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objResult.Value);
        Assert.Equal("UNAUTHENTICATED", error.Code);
    }

    [Fact]
    public async Task UploadBrandingAssets_InvalidBearerFormat_Returns401()
    {
        SetupHttpContext("Basic abc", _tenantId.ToString(), idempotencyKey: "key-1");
        var result = await _controller.UploadBrandingAssets(null, null, null, CancellationToken.None);
        var objResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objResult.Value);
        Assert.Equal("UNAUTHENTICATED", error.Code);
    }

    [Fact]
    public async Task UploadBrandingAssets_TokenValidationFails_Returns401()
    {
        SetupHttpContext("Bearer invalid-token", _tenantId.ToString(), idempotencyKey: "key-1");
        _authServiceMock.Setup(a => a.ValidateTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuthenticatedUser?)null);
        var result = await _controller.UploadBrandingAssets(null, null, null, CancellationToken.None);
        var objResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objResult.Value);
        Assert.Equal("UNAUTHENTICATED", error.Code);
    }

    [Fact]
    public async Task UploadBrandingAssets_MissingTenantHeader_Returns400()
    {
        var jwt = CreateTestJwt();
        SetupHttpContext($"Bearer {jwt}", idempotencyKey: "key-1");
        _authServiceMock.Setup(a => a.ValidateTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticatedUser { UserId = _userId, TenantId = _tenantId, Role = "Admin", Permissions = new HashSet<string>(new[] { "branding.settings.write" }, StringComparer.OrdinalIgnoreCase) });
        var result = await _controller.UploadBrandingAssets(null, null, null, CancellationToken.None);
        var objResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objResult.Value);
        Assert.Equal("INVALID_REQUEST", error.Code);
    }

    [Fact]
    public async Task UploadBrandingAssets_InvalidTenantHeader_Returns400()
    {
        var jwt = CreateTestJwt();
        SetupHttpContext($"Bearer {jwt}", "not-a-guid", idempotencyKey: "key-1");
        _authServiceMock.Setup(a => a.ValidateTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticatedUser { UserId = _userId, TenantId = _tenantId, Role = "Admin", Permissions = new HashSet<string>(new[] { "branding.settings.write" }, StringComparer.OrdinalIgnoreCase) });
        var result = await _controller.UploadBrandingAssets(null, null, null, CancellationToken.None);
        var objResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objResult.Value);
        Assert.Equal("INVALID_REQUEST", error.Code);
    }

    [Fact]
    public async Task UploadBrandingAssets_TenantMismatch_Returns403()
    {
        var jwt = CreateTestJwt();
        var differentTenant = Guid.NewGuid();
        SetupHttpContext($"Bearer {jwt}", differentTenant.ToString(), idempotencyKey: "key-1");
        _authServiceMock.Setup(a => a.ValidateTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticatedUser { UserId = _userId, TenantId = _tenantId, Role = "Admin", Permissions = new HashSet<string>(new[] { "branding.settings.write" }, StringComparer.OrdinalIgnoreCase) });
        var result = await _controller.UploadBrandingAssets(null, null, null, CancellationToken.None);
        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, objResult.StatusCode);
    }

    [Fact]
    public async Task UploadBrandingAssets_NonAdminRole_Returns403()
    {
        SetupValidAuthContext("Consultant");
        var result = await _controller.UploadBrandingAssets(null, null, null, CancellationToken.None);
        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, objResult.StatusCode);
        var error = Assert.IsType<StandardErrorDto>(objResult.Value);
        Assert.Equal("FORBIDDEN", error.Code);
    }

    [Fact]
    public async Task UploadBrandingAssets_MissingIdempotencyKey_Returns400()
    {
        var jwt = CreateTestJwt();
        SetupHttpContext($"Bearer {jwt}", _tenantId.ToString());
        _authServiceMock.Setup(a => a.ValidateTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticatedUser { UserId = _userId, TenantId = _tenantId, Role = "Admin", Permissions = new HashSet<string>(new[] { "branding.settings.write" }, StringComparer.OrdinalIgnoreCase) });
        var result = await _controller.UploadBrandingAssets(null, null, null, CancellationToken.None);
        var objResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objResult.Value);
        Assert.Equal("INVALID_REQUEST_BODY", error.Code);
    }

    [Fact]
    public async Task UploadBrandingAssets_SuccessfulUpload_Returns201()
    {
        SetupValidAuthContext();
        var expectedResponse = new BrandingAssetResponseDto
        {
            Id = "brand-001",
            TenantId = _tenantId.ToString(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Version = 1,
            Etag = "\"1-abc\"",
            Status = "updated"
        };
        _serviceMock.Setup(s => s.UploadBrandingAssetsAsync(
                _tenantId, _userId, It.IsAny<string>(), "idem-key-1",
                It.IsAny<BrandingUploadInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var result = await _controller.UploadBrandingAssets(null, "{\"primary\":\"#000000\"}", null, CancellationToken.None);
        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, objResult.StatusCode);
        var body = Assert.IsType<BrandingAssetResponseDto>(objResult.Value);
        Assert.Equal("brand-001", body.Id);
        Assert.Equal("updated", body.Status);
    }

    [Fact]
    public async Task UploadBrandingAssets_ValidationError_Returns400()
    {
        SetupValidAuthContext();
        _serviceMock.Setup(s => s.UploadBrandingAssetsAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<BrandingUploadInput>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("At least one of logoFile, colorsJson, or fontsZip must be supplied."));

        var result = await _controller.UploadBrandingAssets(null, null, null, CancellationToken.None);
        var objResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objResult.Value);
        Assert.Equal("INVALID_REQUEST", error.Code);
    }

    [Fact]
    public async Task UploadBrandingAssets_IdempotencyConflict_Returns409()
    {
        SetupValidAuthContext();
        _serviceMock.Setup(s => s.UploadBrandingAssetsAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<BrandingUploadInput>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Idempotency-Key has been used with a different payload."));

        var result = await _controller.UploadBrandingAssets(null, "{\"primary\":\"#000\"}", null, CancellationToken.None);
        var objResult = Assert.IsType<ConflictObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objResult.Value);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED_WITH_DIFFERENT_PAYLOAD", error.Code);
    }

    [Fact]
    public async Task UploadBrandingAssets_MalwareDetected_Returns400()
    {
        SetupValidAuthContext();
        _serviceMock.Setup(s => s.UploadBrandingAssetsAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<BrandingUploadInput>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Malware scan detected unsafe content in logoFile. Upload rejected."));

        var result = await _controller.UploadBrandingAssets(null, null, null, CancellationToken.None);
        var objResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objResult.Value);
        Assert.Equal("MALWARE_DETECTED", error.Code);
    }

    [Fact]
    public async Task UploadBrandingAssets_UnexpectedException_Returns500()
    {
        SetupValidAuthContext();
        _serviceMock.Setup(s => s.UploadBrandingAssetsAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<BrandingUploadInput>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection lost"));

        var result = await _controller.UploadBrandingAssets(null, null, null, CancellationToken.None);
        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objResult.StatusCode);
        var error = Assert.IsType<StandardErrorDto>(objResult.Value);
        Assert.Equal("INTERNAL_SERVER_ERROR", error.Code);
    }

    [Fact]
    public async Task UploadBrandingAssets_WithLogoFile_PassesDataToService()
    {
        SetupValidAuthContext();
        var fileContent = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        var formFile = new FormFile(new MemoryStream(fileContent), 0, fileContent.Length, "logoFile", "test.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };

        BrandingUploadInput? capturedInput = null;
        _serviceMock.Setup(s => s.UploadBrandingAssetsAsync(
                _tenantId, _userId, It.IsAny<string>(), "idem-key-1",
                It.IsAny<BrandingUploadInput>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, string, string, BrandingUploadInput, CancellationToken>(
                (_, _, _, _, input, _) => capturedInput = input)
            .ReturnsAsync(new BrandingAssetResponseDto { Id = "b1", Status = "updated", Version = 1 });

        await _controller.UploadBrandingAssets(formFile, null, null, CancellationToken.None);

        Assert.NotNull(capturedInput);
        Assert.NotNull(capturedInput!.LogoData);
        Assert.Equal(fileContent.Length, capturedInput.LogoData!.Length);
        Assert.Equal("test.png", capturedInput.LogoFileName);
        Assert.Equal("image/png", capturedInput.LogoContentType);
    }

    [Fact]
    public async Task UploadBrandingAssets_EmptyRole_Returns403()
    {
        SetupValidAuthContext("");
        var result = await _controller.UploadBrandingAssets(null, null, null, CancellationToken.None);
        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, objResult.StatusCode);
    }
}
