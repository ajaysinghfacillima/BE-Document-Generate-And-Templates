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
/// Error mapping tests for template listing and document review using the specified
/// StandardError codes, and exception-flow tests for branding upload and document
/// generation where formal error contracts are not defined.
/// </summary>
public class ErrorContractTests
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

    private (DefaultHttpContext httpContext, string jwt) CreateContext(
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

    private Mock<IAuthService> CreateAuthMock(string jwt, string role = "Admin")
    {
        var authService = new Mock<IAuthService>();
        authService.Setup(a => a.ValidateTokenAsync(jwt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticatedUser { UserId = _userId, TenantId = _tenantId, Role = role, Permissions = new HashSet<string>(new[] { "documents.generate", "documents.review", "branding.settings.write" }, StringComparer.OrdinalIgnoreCase) });
        return authService;
    }

    // --- Template listing error codes ---

    [Fact]
    public async Task TemplateListing_400_INVALID_REQUEST()
    {
        var jwt = CreateJwt(_userId, _tenantId, "Admin");
        var authService = CreateAuthMock(jwt);
        var service = new Mock<IAdminTemplateService>();
        var logger = new Mock<ILogger<AdminTemplatesController>>();
        var controller = new AdminTemplatesController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = $"Bearer {jwt}";
        httpContext.Request.Headers["X-Tenant-Id"] = "";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.ListTemplates(cancellationToken: CancellationToken.None);
        var objectResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Equal("INVALID_REQUEST", error.Code);
    }

    [Fact]
    public async Task TemplateListing_401_UNAUTHENTICATED()
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
        Assert.Equal("UNAUTHENTICATED", error.Code);
    }

    [Fact]
    public async Task TemplateListing_403_FORBIDDEN()
    {
        var (httpContext, jwt) = CreateContext(role: "Viewer");
        var authService = CreateAuthMock(jwt, role: "Viewer");
        var service = new Mock<IAdminTemplateService>();
        var logger = new Mock<ILogger<AdminTemplatesController>>();
        var controller = new AdminTemplatesController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.ListTemplates(cancellationToken: CancellationToken.None);
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Equal("FORBIDDEN", error.Code);
    }

    [Fact]
    public async Task TemplateListing_500_INTERNAL_SERVER_ERROR()
    {
        var (httpContext, jwt) = CreateContext();
        var authService = CreateAuthMock(jwt);
        var service = new Mock<IAdminTemplateService>();
        service.Setup(s => s.ListTemplatesAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database failure"));
        var logger = new Mock<ILogger<AdminTemplatesController>>();
        var controller = new AdminTemplatesController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.ListTemplates(cancellationToken: CancellationToken.None);
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Equal("INTERNAL_SERVER_ERROR", error.Code);
    }

    // --- Document review error codes ---

    [Fact]
    public async Task DocumentReview_400_INVALID_REVIEW_ACTION()
    {
        var (httpContext, jwt) = CreateContext(idempotencyKey: "key-1", ifMatch: "\"1-abc\"");
        var authService = CreateAuthMock(jwt);
        var service = new Mock<IDocumentReviewService>();
        service.Setup(s => s.ReviewDocumentAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DocumentReviewRequestDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("INVALID_REVIEW_ACTION:Cannot approve a draft document."));
        var logger = new Mock<ILogger<DocumentsReviewController>>();
        var controller = new DocumentsReviewController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new DocumentReviewRequestDto { Action = "approve" };
        var result = await controller.ReviewDocument(
            Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), request, CancellationToken.None);

        var objectResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Equal("INVALID_REVIEW_ACTION", error.Code);
    }

    [Fact]
    public async Task DocumentReview_400_INVALID_REQUEST_BODY()
    {
        var (httpContext, jwt) = CreateContext(idempotencyKey: "key-1", ifMatch: "\"1-abc\"");
        var authService = CreateAuthMock(jwt);
        var service = new Mock<IDocumentReviewService>();
        service.Setup(s => s.ReviewDocumentAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DocumentReviewRequestDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("INVALID_REQUEST_BODY:action is required."));
        var logger = new Mock<ILogger<DocumentsReviewController>>();
        var controller = new DocumentsReviewController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new DocumentReviewRequestDto { Action = "" };
        var result = await controller.ReviewDocument(
            Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), request, CancellationToken.None);

        var objectResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Equal("INVALID_REQUEST_BODY", error.Code);
    }

    [Fact]
    public async Task DocumentReview_404_PROJECT_NOT_FOUND()
    {
        var (httpContext, jwt) = CreateContext(idempotencyKey: "key-1", ifMatch: "\"1-abc\"");
        var authService = CreateAuthMock(jwt);
        var service = new Mock<IDocumentReviewService>();
        service.Setup(s => s.ReviewDocumentAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DocumentReviewRequestDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("PROJECT_NOT_FOUND:Not found."));
        var logger = new Mock<ILogger<DocumentsReviewController>>();
        var controller = new DocumentsReviewController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new DocumentReviewRequestDto { Action = "submit" };
        var result = await controller.ReviewDocument(
            Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), request, CancellationToken.None);

        var objectResult = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Equal("PROJECT_NOT_FOUND", error.Code);
    }

    [Fact]
    public async Task DocumentReview_404_DOCUMENT_NOT_FOUND()
    {
        var (httpContext, jwt) = CreateContext(idempotencyKey: "key-1", ifMatch: "\"1-abc\"");
        var authService = CreateAuthMock(jwt);
        var service = new Mock<IDocumentReviewService>();
        service.Setup(s => s.ReviewDocumentAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DocumentReviewRequestDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("DOCUMENT_NOT_FOUND:Not found."));
        var logger = new Mock<ILogger<DocumentsReviewController>>();
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
    public async Task DocumentReview_409_ETAG_MISMATCH()
    {
        var (httpContext, jwt) = CreateContext(idempotencyKey: "key-1", ifMatch: "\"1-abc\"");
        var authService = CreateAuthMock(jwt);
        var service = new Mock<IDocumentReviewService>();
        service.Setup(s => s.ReviewDocumentAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DocumentReviewRequestDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("ETAG_MISMATCH:Stale concurrency token."));
        var logger = new Mock<ILogger<DocumentsReviewController>>();
        var controller = new DocumentsReviewController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new DocumentReviewRequestDto { Action = "approve" };
        var result = await controller.ReviewDocument(
            Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), request, CancellationToken.None);

        var objectResult = Assert.IsType<ConflictObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Equal("ETAG_MISMATCH", error.Code);
    }

    [Fact]
    public async Task DocumentReview_409_IDEMPOTENCY_KEY_REUSED()
    {
        var (httpContext, jwt) = CreateContext(idempotencyKey: "key-1", ifMatch: "\"1-abc\"");
        var authService = CreateAuthMock(jwt);
        var service = new Mock<IDocumentReviewService>();
        service.Setup(s => s.ReviewDocumentAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DocumentReviewRequestDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Idempotency-Key reused with different payload."));
        var logger = new Mock<ILogger<DocumentsReviewController>>();
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
    public async Task DocumentReview_500_DOCUMENT_REVIEW_PROCESSING_FAILED()
    {
        var (httpContext, jwt) = CreateContext(idempotencyKey: "key-1", ifMatch: "\"1-abc\"");
        var authService = CreateAuthMock(jwt);
        var service = new Mock<IDocumentReviewService>();
        service.Setup(s => s.ReviewDocumentAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DocumentReviewRequestDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Unexpected database failure."));
        var logger = new Mock<ILogger<DocumentsReviewController>>();
        var controller = new DocumentsReviewController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new DocumentReviewRequestDto { Action = "submit" };
        var result = await controller.ReviewDocument(
            Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), request, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Equal("DOCUMENT_REVIEW_PROCESSING_FAILED", error.Code);
    }

    // --- Branding upload exception flows (no formal error contracts) ---

    [Fact]
    public async Task BrandingUpload_ValidationError_Returns400()
    {
        var (httpContext, jwt) = CreateContext(idempotencyKey: "key-1");
        var authService = CreateAuthMock(jwt);
        var service = new Mock<IBrandingAssetService>();
        service.Setup(s => s.UploadBrandingAssetsAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<BrandingUploadInput>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("At least one branding input must be supplied."));
        var logger = new Mock<ILogger<BrandingAssetsController>>();
        var controller = new BrandingAssetsController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.UploadBrandingAssets(null, null, null, CancellationToken.None);
        var objectResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Equal("INVALID_REQUEST", error.Code);
    }

    [Fact]
    public async Task BrandingUpload_MalwareDetected_Returns400()
    {
        var (httpContext, jwt) = CreateContext(idempotencyKey: "key-1");
        var authService = CreateAuthMock(jwt);
        var service = new Mock<IBrandingAssetService>();
        service.Setup(s => s.UploadBrandingAssetsAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<BrandingUploadInput>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Malware detected in uploaded file."));
        var logger = new Mock<ILogger<BrandingAssetsController>>();
        var controller = new BrandingAssetsController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.UploadBrandingAssets(null, "{\"primary\":\"#000\"}", null, CancellationToken.None);
        var objectResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Equal("MALWARE_DETECTED", error.Code);
    }

    [Fact]
    public async Task BrandingUpload_UnexpectedError_Returns500()
    {
        var (httpContext, jwt) = CreateContext(idempotencyKey: "key-1");
        var authService = CreateAuthMock(jwt);
        var service = new Mock<IBrandingAssetService>();
        service.Setup(s => s.UploadBrandingAssetsAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<BrandingUploadInput>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Storage unavailable."));
        var logger = new Mock<ILogger<BrandingAssetsController>>();
        var controller = new BrandingAssetsController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.UploadBrandingAssets(null, "{\"primary\":\"#000\"}", null, CancellationToken.None);
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Equal("INTERNAL_SERVER_ERROR", error.Code);
    }

    // --- Document generation exception flows (no formal error contracts) ---

    [Fact]
    public async Task DocumentGeneration_ValidationError_Returns400()
    {
        var (httpContext, jwt) = CreateContext(idempotencyKey: "key-1");
        var authService = CreateAuthMock(jwt);
        var service = new Mock<IDocumentGenerationService>();
        service.Setup(s => s.GenerateDocumentAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GenerateDocumentRequestDto>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Invalid format specified."));
        var logger = new Mock<ILogger<DocumentsGenerationController>>();
        var controller = new DocumentsGenerationController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new GenerateDocumentRequestDto
        {
            DocumentType = "narrative", Format = "invalid",
            TemplateId = "tmpl-001", SourceIds = new List<string> { "src-001" }
        };
        var result = await controller.GenerateDocument(Guid.NewGuid().ToString(), request, CancellationToken.None);
        var objectResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Equal("INVALID_REQUEST", error.Code);
    }

    [Fact]
    public async Task DocumentGeneration_NotFound_Returns404()
    {
        var (httpContext, jwt) = CreateContext(idempotencyKey: "key-1");
        var authService = CreateAuthMock(jwt);
        var service = new Mock<IDocumentGenerationService>();
        service.Setup(s => s.GenerateDocumentAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GenerateDocumentRequestDto>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Project not found."));
        var logger = new Mock<ILogger<DocumentsGenerationController>>();
        var controller = new DocumentsGenerationController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new GenerateDocumentRequestDto
        {
            DocumentType = "narrative", Format = "pdf",
            TemplateId = "tmpl-001", SourceIds = new List<string> { "src-001" }
        };
        var result = await controller.GenerateDocument(Guid.NewGuid().ToString(), request, CancellationToken.None);
        var objectResult = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Equal("NOT_FOUND", error.Code);
    }

    [Fact]
    public async Task DocumentGeneration_UnexpectedError_Returns500()
    {
        var (httpContext, jwt) = CreateContext(idempotencyKey: "key-1");
        var authService = CreateAuthMock(jwt);
        var service = new Mock<IDocumentGenerationService>();
        service.Setup(s => s.GenerateDocumentAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GenerateDocumentRequestDto>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Rendering engine crashed."));
        var logger = new Mock<ILogger<DocumentsGenerationController>>();
        var controller = new DocumentsGenerationController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new GenerateDocumentRequestDto
        {
            DocumentType = "narrative", Format = "pdf",
            TemplateId = "tmpl-001", SourceIds = new List<string> { "src-001" }
        };
        var result = await controller.GenerateDocument(Guid.NewGuid().ToString(), request, CancellationToken.None);
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Equal("INTERNAL_SERVER_ERROR", error.Code);
    }

    // --- All StandardError responses have traceId ---

    [Theory]
    [InlineData("UNAUTHENTICATED")]
    [InlineData("FORBIDDEN")]
    [InlineData("INVALID_REQUEST")]
    public async Task AllStandardErrors_ContainTraceId(string expectedCode)
    {
        // Using the simplest error trigger for each code
        var service = new Mock<IAdminTemplateService>();
        var authService = new Mock<IAuthService>();
        var logger = new Mock<ILogger<AdminTemplatesController>>();
        var controller = new AdminTemplatesController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);

        var httpContext = new DefaultHttpContext();
        if (expectedCode == "UNAUTHENTICATED")
        {
            // No auth header
        }
        else if (expectedCode == "INVALID_REQUEST")
        {
            var jwt = CreateJwt(_userId, _tenantId, "Admin");
            httpContext.Request.Headers["Authorization"] = $"Bearer {jwt}";
            authService.Setup(a => a.ValidateTokenAsync(jwt, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AuthenticatedUser { UserId = _userId, TenantId = _tenantId, Role = "Admin" });
            // Missing X-Tenant-Id
        }
        else if (expectedCode == "FORBIDDEN")
        {
            var jwt = CreateJwt(_userId, _tenantId, "Viewer");
            httpContext.Request.Headers["Authorization"] = $"Bearer {jwt}";
            httpContext.Request.Headers["X-Tenant-Id"] = _tenantId.ToString();
            authService.Setup(a => a.ValidateTokenAsync(jwt, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AuthenticatedUser { UserId = _userId, TenantId = _tenantId, Role = "Viewer" });
        }

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.ListTemplates(cancellationToken: CancellationToken.None);
        var objectResult = result as ObjectResult;
        Assert.NotNull(objectResult);
        var error = Assert.IsType<StandardErrorDto>(objectResult!.Value);
        Assert.Equal(expectedCode, error.Code);
        Assert.NotNull(error.TraceId);
        Assert.NotEmpty(error.TraceId);
    }
}
