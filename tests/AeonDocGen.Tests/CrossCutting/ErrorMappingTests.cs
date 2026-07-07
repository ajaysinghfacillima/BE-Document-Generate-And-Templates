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
/// Tests StandardError-preserving error mapping across all APIs.
/// Validates that all error responses conform to the StandardErrorDto format
/// with proper TraceId, Code, and Message fields.
/// </summary>
public class ErrorMappingTests
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
        string role = "Admin", string? idempotencyKey = "key-1", string? ifMatch = null)
    {
        var jwt = CreateJwt(_userId, _tenantId, role);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = $"Bearer {jwt}";
        httpContext.Request.Headers["X-Tenant-Id"] = _tenantId.ToString();
        if (idempotencyKey != null)
            httpContext.Request.Headers["Idempotency-Key"] = idempotencyKey;
        if (ifMatch != null)
            httpContext.Request.Headers["If-Match"] = ifMatch;
        return (httpContext, jwt);
    }

    private Mock<IAuthService> CreateAuthServiceMock(string jwt, string role = "Admin")
    {
        var authService = new Mock<IAuthService>();
        authService.Setup(a => a.ValidateTokenAsync(jwt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticatedUser { UserId = _userId, TenantId = _tenantId, Role = role, Permissions = new HashSet<string>(new[] { "documents.generate", "documents.review", "branding.settings.write" }, StringComparer.OrdinalIgnoreCase) });
        return authService;
    }

    private static void AssertStandardError(IActionResult result, int expectedStatusCode, string expectedCode)
    {
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(expectedStatusCode, objectResult.StatusCode);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Equal(expectedCode, error.Code);
        Assert.NotNull(error.TraceId);
        Assert.NotNull(error.Message);
        Assert.NotEmpty(error.Message);
    }

    // --- Authentication errors return StandardError ---

    [Fact]
    public async Task AdminTemplates_MissingAuth_ReturnsStandardError()
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
        Assert.NotNull(error.TraceId);
    }

    [Fact]
    public async Task BrandingAssets_InvalidToken_ReturnsStandardError()
    {
        var jwt = CreateJwt(_userId, _tenantId, "Admin");
        var authService = new Mock<IAuthService>();
        authService.Setup(a => a.ValidateTokenAsync(jwt, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuthenticatedUser?)null);

        var service = new Mock<IBrandingAssetService>();
        var logger = new Mock<ILogger<BrandingAssetsController>>();
        var controller = new BrandingAssetsController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = $"Bearer {jwt}";
        httpContext.Request.Headers["X-Tenant-Id"] = _tenantId.ToString();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.UploadBrandingAssets(null, null, null, CancellationToken.None);

        var objectResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Equal("UNAUTHENTICATED", error.Code);
    }

    // --- Tenant validation errors return StandardError ---

    [Fact]
    public async Task DocumentGeneration_MissingTenantId_ReturnsStandardError()
    {
        var jwt = CreateJwt(_userId, _tenantId, "Admin");
        var authService = CreateAuthServiceMock(jwt);
        var service = new Mock<IDocumentGenerationService>();
        var logger = new Mock<ILogger<DocumentsGenerationController>>();
        var controller = new DocumentsGenerationController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = $"Bearer {jwt}";
        // No X-Tenant-Id
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
        Assert.Equal("INVALID_REQUEST", error.Code);
    }

    [Fact]
    public async Task DocumentGeneration_InvalidTenantIdFormat_ReturnsStandardError()
    {
        var jwt = CreateJwt(_userId, _tenantId, "Admin");
        var authService = CreateAuthServiceMock(jwt);
        var service = new Mock<IDocumentGenerationService>();
        var logger = new Mock<ILogger<DocumentsGenerationController>>();
        var controller = new DocumentsGenerationController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = $"Bearer {jwt}";
        httpContext.Request.Headers["X-Tenant-Id"] = "";
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
        Assert.Equal("INVALID_REQUEST", error.Code);
    }

    // --- Role-based access errors return StandardError ---

    [Fact]
    public async Task AdminTemplates_NonAdminRole_ReturnsForbiddenStandardError()
    {
        var (httpContext, jwt) = CreateAuthenticatedContext(role: "Viewer");
        var authService = CreateAuthServiceMock(jwt, role: "Viewer");
        var service = new Mock<IAdminTemplateService>();
        var logger = new Mock<ILogger<AdminTemplatesController>>();
        var controller = new AdminTemplatesController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.ListTemplates(cancellationToken: CancellationToken.None);

        AssertStandardError(result, StatusCodes.Status403Forbidden, "FORBIDDEN");
    }

    [Fact]
    public async Task BrandingAssets_NonAdminRole_ReturnsForbiddenStandardError()
    {
        var (httpContext, jwt) = CreateAuthenticatedContext(role: "Viewer");
        var authService = CreateAuthServiceMock(jwt, role: "Viewer");
        var service = new Mock<IBrandingAssetService>();
        var logger = new Mock<ILogger<BrandingAssetsController>>();
        var controller = new BrandingAssetsController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.UploadBrandingAssets(null, null, null, CancellationToken.None);

        AssertStandardError(result, StatusCodes.Status403Forbidden, "FORBIDDEN");
    }

    [Fact]
    public async Task DocumentReview_ExternalAuditorRole_ReturnsForbiddenStandardError()
    {
        var (httpContext, jwt) = CreateAuthenticatedContext(
            role: "External Auditor", idempotencyKey: "key-1", ifMatch: "\"1-abc\"");
        var authService = CreateAuthServiceMock(jwt, role: "External Auditor");
        var service = new Mock<IDocumentReviewService>();
        var logger = new Mock<ILogger<DocumentsReviewController>>();
        var controller = new DocumentsReviewController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new DocumentReviewRequestDto { Action = "approve" };
        var result = await controller.ReviewDocument(
            Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), request, CancellationToken.None);

        AssertStandardError(result, StatusCodes.Status403Forbidden, "FORBIDDEN_DOCUMENT_REVIEW");
    }

    // --- Service exception mapping to StandardError ---

    [Fact]
    public async Task DocumentReview_ArgumentException_Returns400StandardError()
    {
        var (httpContext, jwt) = CreateAuthenticatedContext(idempotencyKey: "key-1", ifMatch: "\"1-abc\"");
        var authService = CreateAuthServiceMock(jwt);
        var service = new Mock<IDocumentReviewService>();
        var logger = new Mock<ILogger<DocumentsReviewController>>();

        service.Setup(s => s.ReviewDocumentAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DocumentReviewRequestDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("INVALID_REVIEW_ACTION:Cannot approve a draft document directly."));

        var controller = new DocumentsReviewController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new DocumentReviewRequestDto { Action = "approve" };
        var result = await controller.ReviewDocument(
            Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), request, CancellationToken.None);

        var objectResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Equal("INVALID_REVIEW_ACTION", error.Code);
        Assert.Equal("Cannot approve a draft document directly.", error.Message);
    }

    [Fact]
    public async Task DocumentReview_ProjectNotFound_Returns404StandardError()
    {
        var (httpContext, jwt) = CreateAuthenticatedContext(idempotencyKey: "key-1", ifMatch: "\"1-abc\"");
        var authService = CreateAuthServiceMock(jwt);
        var service = new Mock<IDocumentReviewService>();
        var logger = new Mock<ILogger<DocumentsReviewController>>();

        service.Setup(s => s.ReviewDocumentAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DocumentReviewRequestDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("PROJECT_NOT_FOUND:The specified project does not exist."));

        var controller = new DocumentsReviewController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new DocumentReviewRequestDto { Action = "submit" };
        var result = await controller.ReviewDocument(
            Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), request, CancellationToken.None);

        var objectResult = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Equal("PROJECT_NOT_FOUND", error.Code);
        Assert.Equal("The specified project does not exist.", error.Message);
    }

    [Fact]
    public async Task DocumentReview_DocumentNotFound_Returns404StandardError()
    {
        var (httpContext, jwt) = CreateAuthenticatedContext(idempotencyKey: "key-1", ifMatch: "\"1-abc\"");
        var authService = CreateAuthServiceMock(jwt);
        var service = new Mock<IDocumentReviewService>();
        var logger = new Mock<ILogger<DocumentsReviewController>>();

        service.Setup(s => s.ReviewDocumentAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DocumentReviewRequestDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("DOCUMENT_NOT_FOUND:The specified document does not exist."));

        var controller = new DocumentsReviewController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new DocumentReviewRequestDto { Action = "submit" };
        var result = await controller.ReviewDocument(
            Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), request, CancellationToken.None);

        var objectResult = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Equal("DOCUMENT_NOT_FOUND", error.Code);
    }

    [Fact]
    public async Task DocumentGeneration_ArgumentException_Returns400StandardError()
    {
        var (httpContext, jwt) = CreateAuthenticatedContext(idempotencyKey: "key-1");
        var authService = CreateAuthServiceMock(jwt);
        var service = new Mock<IDocumentGenerationService>();
        var logger = new Mock<ILogger<DocumentsGenerationController>>();

        service.Setup(s => s.GenerateDocumentAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GenerateDocumentRequestDto>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Invalid document type specified."));

        var controller = new DocumentsGenerationController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new GenerateDocumentRequestDto
        {
            DocumentType = "invalid",
            Format = "pdf",
            TemplateId = "tmpl-001",
            SourceIds = new List<string> { "src-001" }
        };

        var result = await controller.GenerateDocument(Guid.NewGuid().ToString(), request, CancellationToken.None);

        var objectResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Equal("INVALID_REQUEST", error.Code);
    }

    [Fact]
    public async Task DocumentGeneration_ResourceNotFound_Returns404StandardError()
    {
        var (httpContext, jwt) = CreateAuthenticatedContext(idempotencyKey: "key-1");
        var authService = CreateAuthServiceMock(jwt);
        var service = new Mock<IDocumentGenerationService>();
        var logger = new Mock<ILogger<DocumentsGenerationController>>();

        service.Setup(s => s.GenerateDocumentAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GenerateDocumentRequestDto>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Template not found."));

        var controller = new DocumentsGenerationController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new GenerateDocumentRequestDto
        {
            DocumentType = "narrative",
            Format = "pdf",
            TemplateId = "tmpl-missing",
            SourceIds = new List<string> { "src-001" }
        };

        var result = await controller.GenerateDocument(Guid.NewGuid().ToString(), request, CancellationToken.None);

        var objectResult = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Equal("NOT_FOUND", error.Code);
    }

    [Fact]
    public async Task BrandingAssets_MalwareDetected_Returns400StandardError()
    {
        var (httpContext, jwt) = CreateAuthenticatedContext(idempotencyKey: "key-1");
        var authService = CreateAuthServiceMock(jwt);
        var service = new Mock<IBrandingAssetService>();
        var logger = new Mock<ILogger<BrandingAssetsController>>();

        service.Setup(s => s.UploadBrandingAssetsAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<BrandingUploadInput>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Malware detected in uploaded logo file."));

        var controller = new BrandingAssetsController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.UploadBrandingAssets(null, "{\"primary\":\"#000\"}", null, CancellationToken.None);

        var objectResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Equal("MALWARE_DETECTED", error.Code);
    }

    // --- Unexpected exceptions return StandardError ---

    [Fact]
    public async Task AdminTemplates_UnexpectedException_Returns500StandardError()
    {
        var (httpContext, jwt) = CreateAuthenticatedContext();
        var authService = CreateAuthServiceMock(jwt);
        var service = new Mock<IAdminTemplateService>();
        var logger = new Mock<ILogger<AdminTemplatesController>>();

        service.Setup(s => s.ListTemplatesAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        var controller = new AdminTemplatesController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.ListTemplates(cancellationToken: CancellationToken.None);

        AssertStandardError(result, StatusCodes.Status500InternalServerError, "INTERNAL_SERVER_ERROR");
    }

    [Fact]
    public async Task DocumentReview_UnexpectedException_Returns500StandardError()
    {
        var (httpContext, jwt) = CreateAuthenticatedContext(idempotencyKey: "key-1", ifMatch: "\"1-abc\"");
        var authService = CreateAuthServiceMock(jwt);
        var service = new Mock<IDocumentReviewService>();
        var logger = new Mock<ILogger<DocumentsReviewController>>();

        service.Setup(s => s.ReviewDocumentAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DocumentReviewRequestDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Unexpected failure"));

        var controller = new DocumentsReviewController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new DocumentReviewRequestDto { Action = "submit" };
        var result = await controller.ReviewDocument(
            Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), request, CancellationToken.None);

        AssertStandardError(result, StatusCodes.Status500InternalServerError, "DOCUMENT_REVIEW_PROCESSING_FAILED");
    }

    [Fact]
    public async Task DocumentGeneration_UnexpectedException_Returns500StandardError()
    {
        var (httpContext, jwt) = CreateAuthenticatedContext(idempotencyKey: "key-1");
        var authService = CreateAuthServiceMock(jwt);
        var service = new Mock<IDocumentGenerationService>();
        var logger = new Mock<ILogger<DocumentsGenerationController>>();

        service.Setup(s => s.GenerateDocumentAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GenerateDocumentRequestDto>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Unexpected failure"));

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

        AssertStandardError(result, StatusCodes.Status500InternalServerError, "INTERNAL_SERVER_ERROR");
    }

    [Fact]
    public async Task BrandingAssets_UnexpectedException_Returns500StandardError()
    {
        var (httpContext, jwt) = CreateAuthenticatedContext(idempotencyKey: "key-1");
        var authService = CreateAuthServiceMock(jwt);
        var service = new Mock<IBrandingAssetService>();
        var logger = new Mock<ILogger<BrandingAssetsController>>();

        service.Setup(s => s.UploadBrandingAssetsAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<BrandingUploadInput>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Storage failure"));

        var controller = new BrandingAssetsController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.UploadBrandingAssets(null, "{\"primary\":\"#000\"}", null, CancellationToken.None);

        AssertStandardError(result, StatusCodes.Status500InternalServerError, "INTERNAL_SERVER_ERROR");
    }

    // --- Invalid route params return StandardError ---

    [Fact]
    public async Task DocumentGeneration_InvalidProjectId_ReturnsStandardError()
    {
        var (httpContext, jwt) = CreateAuthenticatedContext(idempotencyKey: "key-1");
        var authService = CreateAuthServiceMock(jwt);
        var service = new Mock<IDocumentGenerationService>();
        var logger = new Mock<ILogger<DocumentsGenerationController>>();
        var controller = new DocumentsGenerationController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new GenerateDocumentRequestDto
        {
            DocumentType = "narrative",
            Format = "pdf",
            TemplateId = "tmpl-001",
            SourceIds = new List<string> { "src-001" }
        };

        var result = await controller.GenerateDocument("", request, CancellationToken.None);

        var objectResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Equal("INVALID_REQUEST", error.Code);
    }

    [Fact]
    public async Task DocumentReview_InvalidProjectId_ReturnsStandardError()
    {
        var (httpContext, jwt) = CreateAuthenticatedContext(idempotencyKey: "key-1", ifMatch: "\"1-abc\"");
        var authService = CreateAuthServiceMock(jwt);
        var service = new Mock<IDocumentReviewService>();
        var logger = new Mock<ILogger<DocumentsReviewController>>();
        var controller = new DocumentsReviewController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new DocumentReviewRequestDto { Action = "submit" };
        var result = await controller.ReviewDocument(
            "", Guid.NewGuid().ToString(), request, CancellationToken.None);

        var objectResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Equal("INVALID_REQUEST", error.Code);
    }

    [Fact]
    public async Task DocumentReview_InvalidDocumentId_ReturnsStandardError()
    {
        var (httpContext, jwt) = CreateAuthenticatedContext(idempotencyKey: "key-1", ifMatch: "\"1-abc\"");
        var authService = CreateAuthServiceMock(jwt);
        var service = new Mock<IDocumentReviewService>();
        var logger = new Mock<ILogger<DocumentsReviewController>>();
        var controller = new DocumentsReviewController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new DocumentReviewRequestDto { Action = "submit" };
        var result = await controller.ReviewDocument(
            Guid.NewGuid().ToString(), "", request, CancellationToken.None);

        var objectResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Equal("INVALID_REQUEST", error.Code);
    }

    // --- Error responses always contain TraceId ---

    [Fact]
    public async Task AllErrors_ContainNonEmptyTraceId()
    {
        var service = new Mock<IAdminTemplateService>();
        var authService = new Mock<IAuthService>();
        var logger = new Mock<ILogger<AdminTemplatesController>>();
        var controller = new AdminTemplatesController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);

        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.ListTemplates(cancellationToken: CancellationToken.None);

        var objectResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.NotNull(error.TraceId);
        Assert.NotEmpty(error.TraceId);
    }
}
