// TR: LLD-0029 | ORIGIN: MID_LEVEL_DESIGN-0029
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AeonDocGen.Core.DTOs;
using AeonDocGen.Core.Interfaces;
using AeonDocGen.Api.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace AeonDocGen.Tests.CrossCutting;

/// <summary>
/// Tests idempotency enforcement across all POST APIs.
/// Validates Idempotency-Key header requirement, replay of successful responses,
/// and payload mismatch detection.
/// </summary>
public class IdempotencyTests
{
    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-00000000000A");
    private readonly Guid _userId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private string CreateJwt(Guid userId, Guid tenantId, string role)
    {
        var handler = new JwtSecurityTokenHandler();
        var token = new JwtSecurityToken(
            claims: new[]
            {
                new Claim("sub", userId.ToString()),
                new Claim("tenant_id", tenantId.ToString()),
                new Claim("role", role)
            });
        return handler.WriteToken(token);
    }

    private (DefaultHttpContext httpContext, string jwt) CreateAuthenticatedContext(
        string idempotencyKey, string? ifMatch = null)
    {
        var jwt = CreateJwt(_userId, _tenantId, "Admin");
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = $"Bearer {jwt}";
        httpContext.Request.Headers["X-Tenant-Id"] = _tenantId.ToString();
        httpContext.Request.Headers["Idempotency-Key"] = idempotencyKey;
        if (ifMatch != null)
            httpContext.Request.Headers["If-Match"] = ifMatch;
        return (httpContext, jwt);
    }

    private Mock<IAuthService> CreateAuthServiceMock(string jwt)
    {
        var authService = new Mock<IAuthService>();
        authService.Setup(a => a.ValidateTokenAsync(jwt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticatedUser
            {
                UserId = _userId,
                TenantId = _tenantId,
                Role = "Admin",
                Permissions = new HashSet<string>(new[] { "documents.generate", "documents.review", "branding.settings.write" }, StringComparer.OrdinalIgnoreCase)
            });
        return authService;
    }

    // --- BrandingAssetsController idempotency ---

    [Fact]
    public async Task BrandingAssets_MissingIdempotencyKey_Returns400()
    {
        var jwt = CreateJwt(_userId, _tenantId, "Admin");
        var authService = CreateAuthServiceMock(jwt);
        var service = new Mock<IBrandingAssetService>();
        var logger = new Mock<ILogger<BrandingAssetsController>>();
        var controller = new BrandingAssetsController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = $"Bearer {jwt}";
        httpContext.Request.Headers["X-Tenant-Id"] = _tenantId.ToString();
        // No Idempotency-Key header
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.UploadBrandingAssets(null, null, null, CancellationToken.None);

        var objectResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Equal("INVALID_REQUEST_BODY", error.Code);
    }

    [Fact]
    public async Task BrandingAssets_IdempotencyKeyConflict_Returns409()
    {
        var (httpContext, jwt) = CreateAuthenticatedContext("key-001");
        var authService = CreateAuthServiceMock(jwt);
        var service = new Mock<IBrandingAssetService>();
        var logger = new Mock<ILogger<BrandingAssetsController>>();

        service.Setup(s => s.UploadBrandingAssetsAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<BrandingUploadInput>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Idempotency-Key 'key-001' was already used with a different payload."));

        var controller = new BrandingAssetsController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.UploadBrandingAssets(null, "{\"primary\":\"#000\"}", null, CancellationToken.None);

        var objectResult = Assert.IsType<ConflictObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED_WITH_DIFFERENT_PAYLOAD", error.Code);
    }

    [Fact]
    public async Task BrandingAssets_IdempotencyKeyReplay_Returns201()
    {
        var (httpContext, jwt) = CreateAuthenticatedContext("key-replay");
        var authService = CreateAuthServiceMock(jwt);
        var service = new Mock<IBrandingAssetService>();
        var logger = new Mock<ILogger<BrandingAssetsController>>();

        var replayResponse = new BrandingAssetResponseDto
        {
            Id = Guid.NewGuid().ToString(),
            TenantId = _tenantId.ToString(),
            Status = "updated",
            Etag = "\"1-abc\""
        };

        service.Setup(s => s.UploadBrandingAssetsAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<BrandingUploadInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(replayResponse);

        var controller = new BrandingAssetsController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.UploadBrandingAssets(null, "{\"primary\":\"#000\"}", null, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, objectResult.StatusCode);
    }

    // --- DocumentsGenerationController idempotency ---

    [Fact]
    public async Task DocumentGeneration_MissingIdempotencyKey_Returns400()
    {
        var jwt = CreateJwt(_userId, _tenantId, "Admin");
        var authService = CreateAuthServiceMock(jwt);
        var service = new Mock<IDocumentGenerationService>();
        var logger = new Mock<ILogger<DocumentsGenerationController>>();

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = $"Bearer {jwt}";
        httpContext.Request.Headers["X-Tenant-Id"] = _tenantId.ToString();
        // No Idempotency-Key

        var controller = new DocumentsGenerationController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new GenerateDocumentRequestDto
        {
            DocumentType = "narrative",
            Format = "pdf",
            TemplateId = "tmpl-001",
            SourceIds = new List<string> { "src-001" }
        };

        var result = await controller.GenerateDocument(Guid.NewGuid().ToString(), request, CancellationToken.None);

        var objectResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Equal("INVALID_REQUEST_BODY", error.Code);
        Assert.Contains("Idempotency-Key", error.Message);
    }

    [Fact]
    public async Task DocumentGeneration_IdempotencyKeyConflict_Returns409()
    {
        var (httpContext, jwt) = CreateAuthenticatedContext("key-gen-001");
        var authService = CreateAuthServiceMock(jwt);
        var service = new Mock<IDocumentGenerationService>();
        var logger = new Mock<ILogger<DocumentsGenerationController>>();

        service.Setup(s => s.GenerateDocumentAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GenerateDocumentRequestDto>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Idempotency-Key 'key-gen-001' was already used with a different payload."));

        var controller = new DocumentsGenerationController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new GenerateDocumentRequestDto
        {
            DocumentType = "narrative",
            Format = "pdf",
            TemplateId = "tmpl-001",
            SourceIds = new List<string> { "src-001" }
        };

        var result = await controller.GenerateDocument(Guid.NewGuid().ToString(), request, CancellationToken.None);

        var objectResult = Assert.IsType<ConflictObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED_WITH_DIFFERENT_PAYLOAD", error.Code);
    }

    // --- DocumentsReviewController idempotency ---

    [Fact]
    public async Task DocumentReview_MissingIdempotencyKey_Returns400()
    {
        var jwt = CreateJwt(_userId, _tenantId, "Admin");
        var authService = CreateAuthServiceMock(jwt);
        var service = new Mock<IDocumentReviewService>();
        var logger = new Mock<ILogger<DocumentsReviewController>>();
        var controller = new DocumentsReviewController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = $"Bearer {jwt}";
        httpContext.Request.Headers["X-Tenant-Id"] = _tenantId.ToString();
        httpContext.Request.Headers["If-Match"] = "\"1-abc\"";
        // No Idempotency-Key
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new DocumentReviewRequestDto { Action = "submit" };
        var result = await controller.ReviewDocument(
            Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), request, CancellationToken.None);

        var objectResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Contains("Idempotency-Key", error.Message);
    }

    [Fact]
    public async Task DocumentReview_IdempotencyKeyConflict_Returns409()
    {
        var (httpContext, jwt) = CreateAuthenticatedContext("key-review-001", "\"1-abc\"");
        var authService = CreateAuthServiceMock(jwt);
        var service = new Mock<IDocumentReviewService>();
        var logger = new Mock<ILogger<DocumentsReviewController>>();

        service.Setup(s => s.ReviewDocumentAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DocumentReviewRequestDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Idempotency-Key 'key-review-001' was already used with a different payload."));

        var controller = new DocumentsReviewController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new DocumentReviewRequestDto { Action = "submit" };
        var result = await controller.ReviewDocument(
            Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), request, CancellationToken.None);

        var objectResult = Assert.IsType<ConflictObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED_WITH_DIFFERENT_PAYLOAD", error.Code);
    }

    [Fact]
    public async Task DocumentReview_IdempotencyKeyReplay_Returns200()
    {
        var (httpContext, jwt) = CreateAuthenticatedContext("key-review-replay", "\"1-abc\"");
        var authService = CreateAuthServiceMock(jwt);
        var service = new Mock<IDocumentReviewService>();
        var logger = new Mock<ILogger<DocumentsReviewController>>();

        var replayResponse = new DocumentReviewResponseDto
        {
            DocumentId = Guid.NewGuid().ToString(),
            ProjectId = Guid.NewGuid().ToString(),
            ReviewStatus = "approved",
            Etag = "\"2-def\""
        };

        service.Setup(s => s.ReviewDocumentAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DocumentReviewRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(replayResponse);

        var controller = new DocumentsReviewController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new DocumentReviewRequestDto { Action = "approve" };
        var result = await controller.ReviewDocument(
            Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), request, CancellationToken.None);

        var objectResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<DocumentReviewResponseDto>(objectResult.Value);
        Assert.Equal("approved", response.ReviewStatus);
    }

    // --- Idempotency-Key format and edge cases ---

    [Fact]
    public async Task BrandingAssets_EmptyIdempotencyKey_Returns400()
    {
        var jwt = CreateJwt(_userId, _tenantId, "Admin");
        var authService = CreateAuthServiceMock(jwt);
        var service = new Mock<IBrandingAssetService>();
        var logger = new Mock<ILogger<BrandingAssetsController>>();
        var controller = new BrandingAssetsController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = $"Bearer {jwt}";
        httpContext.Request.Headers["X-Tenant-Id"] = _tenantId.ToString();
        httpContext.Request.Headers["Idempotency-Key"] = "";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.UploadBrandingAssets(null, null, null, CancellationToken.None);

        var objectResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Equal("INVALID_REQUEST_BODY", error.Code);
    }

    [Fact]
    public async Task DocumentReview_WhitespaceIdempotencyKey_Returns400()
    {
        var jwt = CreateJwt(_userId, _tenantId, "Admin");
        var authService = CreateAuthServiceMock(jwt);
        var service = new Mock<IDocumentReviewService>();
        var logger = new Mock<ILogger<DocumentsReviewController>>();
        var controller = new DocumentsReviewController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = $"Bearer {jwt}";
        httpContext.Request.Headers["X-Tenant-Id"] = _tenantId.ToString();
        httpContext.Request.Headers["If-Match"] = "\"1-abc\"";
        httpContext.Request.Headers["Idempotency-Key"] = "   ";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new DocumentReviewRequestDto { Action = "submit" };
        var result = await controller.ReviewDocument(
            Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), request, CancellationToken.None);

        var objectResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Contains("Idempotency-Key", error.Message);
    }

    // --- Service receives idempotency key ---

    [Fact]
    public async Task DocumentGeneration_IdempotencyKeyPassedToService()
    {
        var (httpContext, jwt) = CreateAuthenticatedContext("key-verify-pass");
        var authService = CreateAuthServiceMock(jwt);
        var service = new Mock<IDocumentGenerationService>();
        var logger = new Mock<ILogger<DocumentsGenerationController>>();

        string? capturedKey = null;
        service.Setup(s => s.GenerateDocumentAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GenerateDocumentRequestDto>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, Guid, string, string, GenerateDocumentRequestDto, CancellationToken>(
                (_, _, _, _, key, _, _) => capturedKey = key)
            .ReturnsAsync(new DocumentArtifactResponseDto());

        var controller = new DocumentsGenerationController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new GenerateDocumentRequestDto
        {
            DocumentType = "narrative",
            Format = "pdf",
            TemplateId = "tmpl-001",
            SourceIds = new List<string> { "src-001" }
        };

        await controller.GenerateDocument(Guid.NewGuid().ToString(), request, CancellationToken.None);

        Assert.Equal("key-verify-pass", capturedKey);
    }

    [Fact]
    public async Task BrandingAssets_IdempotencyKeyPassedToService()
    {
        var (httpContext, jwt) = CreateAuthenticatedContext("key-brand-pass");
        var authService = CreateAuthServiceMock(jwt);
        var service = new Mock<IBrandingAssetService>();
        var logger = new Mock<ILogger<BrandingAssetsController>>();

        string? capturedKey = null;
        service.Setup(s => s.UploadBrandingAssetsAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<BrandingUploadInput>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, string, string, BrandingUploadInput, CancellationToken>(
                (_, _, _, key, _, _) => capturedKey = key)
            .ReturnsAsync(new BrandingAssetResponseDto());

        var controller = new BrandingAssetsController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        await controller.UploadBrandingAssets(null, "{\"primary\":\"#000\"}", null, CancellationToken.None);

        Assert.Equal("key-brand-pass", capturedKey);
    }
}
