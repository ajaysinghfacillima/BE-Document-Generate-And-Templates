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

namespace AeonDocGen.Tests.Contracts;

/// <summary>
/// Header validation tests covering required and optional headers exactly
/// as specified for each endpoint.
/// </summary>
public class HeaderValidationTests
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

    private Mock<IAuthService> CreateAuthServiceMock(string jwt, string role = "Admin")
    {
        var authService = new Mock<IAuthService>();
        authService.Setup(a => a.ValidateTokenAsync(jwt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticatedUser { UserId = _userId, TenantId = _tenantId, Role = role, Permissions = new HashSet<string>(new[] { "documents.generate", "documents.review", "branding.settings.write" }, StringComparer.OrdinalIgnoreCase) });
        return authService;
    }

    // --- AdminTemplates: Authorization required, X-Tenant-Id required, X-Correlation-Id optional ---

    [Fact]
    public async Task AdminTemplates_RequiresAuthorizationHeader()
    {
        var service = new Mock<IAdminTemplateService>();
        var authService = new Mock<IAuthService>();
        var logger = new Mock<ILogger<AdminTemplatesController>>();
        var controller = new AdminTemplatesController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Tenant-Id"] = _tenantId.ToString();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.ListTemplates(cancellationToken: CancellationToken.None);
        var objectResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Equal("UNAUTHENTICATED", error.Code);
    }

    [Fact]
    public async Task AdminTemplates_RequiresTenantIdHeader()
    {
        var jwt = CreateJwt(_userId, _tenantId, "Admin");
        var authService = CreateAuthServiceMock(jwt);
        var service = new Mock<IAdminTemplateService>();
        var logger = new Mock<ILogger<AdminTemplatesController>>();
        var controller = new AdminTemplatesController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = $"Bearer {jwt}";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.ListTemplates(cancellationToken: CancellationToken.None);
        var objectResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Equal("INVALID_REQUEST", error.Code);
    }

    [Fact]
    public async Task AdminTemplates_OptionalCorrelationId_Succeeds()
    {
        var jwt = CreateJwt(_userId, _tenantId, "Admin");
        var authService = CreateAuthServiceMock(jwt);
        var service = new Mock<IAdminTemplateService>();
        service.Setup(s => s.ListTemplatesAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminTemplateListResponseDto());
        var logger = new Mock<ILogger<AdminTemplatesController>>();
        var controller = new AdminTemplatesController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = $"Bearer {jwt}";
        httpContext.Request.Headers["X-Tenant-Id"] = _tenantId.ToString();
        // No X-Correlation-Id - should succeed
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.ListTemplates(cancellationToken: CancellationToken.None);
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task AdminTemplates_WithCorrelationId_Succeeds()
    {
        var jwt = CreateJwt(_userId, _tenantId, "Admin");
        var authService = CreateAuthServiceMock(jwt);
        var service = new Mock<IAdminTemplateService>();
        service.Setup(s => s.ListTemplatesAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminTemplateListResponseDto());
        var logger = new Mock<ILogger<AdminTemplatesController>>();
        var controller = new AdminTemplatesController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = $"Bearer {jwt}";
        httpContext.Request.Headers["X-Tenant-Id"] = _tenantId.ToString();
        httpContext.Request.Headers["X-Correlation-Id"] = "corr-test-123";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.ListTemplates(cancellationToken: CancellationToken.None);
        Assert.IsType<OkObjectResult>(result);
    }

    // --- BrandingAssets: Authorization, X-Tenant-Id, Idempotency-Key required; X-Correlation-Id optional ---

    [Fact]
    public async Task BrandingAssets_RequiresAuthorizationHeader()
    {
        var service = new Mock<IBrandingAssetService>();
        var authService = new Mock<IAuthService>();
        var logger = new Mock<ILogger<BrandingAssetsController>>();
        var controller = new BrandingAssetsController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Tenant-Id"] = _tenantId.ToString();
        httpContext.Request.Headers["Idempotency-Key"] = "key-1";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.UploadBrandingAssets(null, null, null, CancellationToken.None);
        var objectResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Equal("UNAUTHENTICATED", error.Code);
    }

    [Fact]
    public async Task BrandingAssets_RequiresTenantIdHeader()
    {
        var jwt = CreateJwt(_userId, _tenantId, "Admin");
        var authService = CreateAuthServiceMock(jwt);
        var service = new Mock<IBrandingAssetService>();
        var logger = new Mock<ILogger<BrandingAssetsController>>();
        var controller = new BrandingAssetsController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = $"Bearer {jwt}";
        httpContext.Request.Headers["Idempotency-Key"] = "key-1";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.UploadBrandingAssets(null, null, null, CancellationToken.None);
        var objectResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Equal("INVALID_REQUEST", error.Code);
    }

    [Fact]
    public async Task BrandingAssets_RequiresIdempotencyKeyHeader()
    {
        var jwt = CreateJwt(_userId, _tenantId, "Admin");
        var authService = CreateAuthServiceMock(jwt);
        var service = new Mock<IBrandingAssetService>();
        var logger = new Mock<ILogger<BrandingAssetsController>>();
        var controller = new BrandingAssetsController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = $"Bearer {jwt}";
        httpContext.Request.Headers["X-Tenant-Id"] = _tenantId.ToString();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.UploadBrandingAssets(null, null, null, CancellationToken.None);
        var objectResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Equal("INVALID_REQUEST_BODY", error.Code);
    }

    // --- DocumentGeneration: Authorization, X-Tenant-Id, Idempotency-Key required; X-Correlation-Id optional ---

    [Fact]
    public async Task DocumentGeneration_RequiresAuthorizationHeader()
    {
        var service = new Mock<IDocumentGenerationService>();
        var authService = new Mock<IAuthService>();
        var logger = new Mock<ILogger<DocumentsGenerationController>>();
        var controller = new DocumentsGenerationController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Tenant-Id"] = _tenantId.ToString();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new GenerateDocumentRequestDto
        {
            DocumentType = "narrative", Format = "pdf",
            TemplateId = "tmpl-001", SourceIds = new List<string> { "src-001" }
        };
        var result = await controller.GenerateDocument(Guid.NewGuid().ToString(), request, CancellationToken.None);
        var objectResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Equal("UNAUTHENTICATED", error.Code);
    }

    [Fact]
    public async Task DocumentGeneration_RequiresTenantIdHeader()
    {
        var jwt = CreateJwt(_userId, _tenantId, "Admin");
        var authService = CreateAuthServiceMock(jwt);
        var service = new Mock<IDocumentGenerationService>();
        var logger = new Mock<ILogger<DocumentsGenerationController>>();
        var controller = new DocumentsGenerationController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = $"Bearer {jwt}";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new GenerateDocumentRequestDto
        {
            DocumentType = "narrative", Format = "pdf",
            TemplateId = "tmpl-001", SourceIds = new List<string> { "src-001" }
        };
        var result = await controller.GenerateDocument(Guid.NewGuid().ToString(), request, CancellationToken.None);
        var objectResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Equal("INVALID_REQUEST", error.Code);
    }

    [Fact]
    public async Task DocumentGeneration_RequiresIdempotencyKeyHeader()
    {
        var jwt = CreateJwt(_userId, _tenantId, "Admin");
        var authService = CreateAuthServiceMock(jwt);
        var service = new Mock<IDocumentGenerationService>();
        var logger = new Mock<ILogger<DocumentsGenerationController>>();
        var controller = new DocumentsGenerationController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = $"Bearer {jwt}";
        httpContext.Request.Headers["X-Tenant-Id"] = _tenantId.ToString();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new GenerateDocumentRequestDto
        {
            DocumentType = "narrative", Format = "pdf",
            TemplateId = "tmpl-001", SourceIds = new List<string> { "src-001" }
        };
        var result = await controller.GenerateDocument(Guid.NewGuid().ToString(), request, CancellationToken.None);
        var objectResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Equal("INVALID_REQUEST_BODY", error.Code);
        Assert.Contains("Idempotency-Key", error.Message);
    }

    // --- DocumentReview: Authorization, X-Tenant-Id, Idempotency-Key, If-Match all required ---

    [Fact]
    public async Task DocumentReview_RequiresAuthorizationHeader()
    {
        var service = new Mock<IDocumentReviewService>();
        var authService = new Mock<IAuthService>();
        var logger = new Mock<ILogger<DocumentsReviewController>>();
        var controller = new DocumentsReviewController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Tenant-Id"] = _tenantId.ToString();
        httpContext.Request.Headers["Idempotency-Key"] = "key-1";
        httpContext.Request.Headers["If-Match"] = "\"1-abc\"";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new DocumentReviewRequestDto { Action = "submit" };
        var result = await controller.ReviewDocument(
            Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), request, CancellationToken.None);
        var objectResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Equal("UNAUTHENTICATED", error.Code);
    }

    [Fact]
    public async Task DocumentReview_RequiresTenantIdHeader()
    {
        var jwt = CreateJwt(_userId, _tenantId, "Admin");
        var authService = CreateAuthServiceMock(jwt);
        var service = new Mock<IDocumentReviewService>();
        var logger = new Mock<ILogger<DocumentsReviewController>>();
        var controller = new DocumentsReviewController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = $"Bearer {jwt}";
        httpContext.Request.Headers["Idempotency-Key"] = "key-1";
        httpContext.Request.Headers["If-Match"] = "\"1-abc\"";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new DocumentReviewRequestDto { Action = "submit" };
        var result = await controller.ReviewDocument(
            Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), request, CancellationToken.None);
        var objectResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Equal("INVALID_REQUEST", error.Code);
    }

    [Fact]
    public async Task DocumentReview_RequiresIdempotencyKeyHeader()
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
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new DocumentReviewRequestDto { Action = "submit" };
        var result = await controller.ReviewDocument(
            Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), request, CancellationToken.None);
        var objectResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Contains("Idempotency-Key", error.Message);
    }

    [Fact]
    public async Task DocumentReview_RequiresIfMatchHeader()
    {
        var jwt = CreateJwt(_userId, _tenantId, "Admin");
        var authService = CreateAuthServiceMock(jwt);
        var service = new Mock<IDocumentReviewService>();
        var logger = new Mock<ILogger<DocumentsReviewController>>();
        var controller = new DocumentsReviewController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = $"Bearer {jwt}";
        httpContext.Request.Headers["X-Tenant-Id"] = _tenantId.ToString();
        httpContext.Request.Headers["Idempotency-Key"] = "key-1";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new DocumentReviewRequestDto { Action = "submit" };
        var result = await controller.ReviewDocument(
            Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), request, CancellationToken.None);
        var objectResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Contains("If-Match", error.Message);
    }

    [Fact]
    public async Task DocumentReview_OptionalCorrelationId_Succeeds()
    {
        var jwt = CreateJwt(_userId, _tenantId, "Admin");
        var authService = CreateAuthServiceMock(jwt);
        var service = new Mock<IDocumentReviewService>();
        service.Setup(s => s.ReviewDocumentAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DocumentReviewRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentReviewResponseDto());
        var logger = new Mock<ILogger<DocumentsReviewController>>();
        var controller = new DocumentsReviewController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = $"Bearer {jwt}";
        httpContext.Request.Headers["X-Tenant-Id"] = _tenantId.ToString();
        httpContext.Request.Headers["Idempotency-Key"] = "key-1";
        httpContext.Request.Headers["If-Match"] = "\"1-abc\"";
        // No X-Correlation-Id
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new DocumentReviewRequestDto { Action = "submit" };
        var result = await controller.ReviewDocument(
            Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), request, CancellationToken.None);
        Assert.IsType<OkObjectResult>(result);
    }
}
