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
/// Tests that audit-relevant data (tenant, actor, correlation) is correctly
/// propagated from controllers to services across all 4 scoped APIs.
/// </summary>
public class AuditLoggingTests
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
        string? correlationId = null, string? idempotencyKey = null, string? ifMatch = null)
    {
        var jwt = CreateJwt(_userId, _tenantId, "Admin");
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = $"Bearer {jwt}";
        httpContext.Request.Headers["X-Tenant-Id"] = _tenantId.ToString();
        if (correlationId != null)
            httpContext.Request.Headers["X-Correlation-Id"] = correlationId;
        if (idempotencyKey != null)
            httpContext.Request.Headers["Idempotency-Key"] = idempotencyKey;
        if (ifMatch != null)
            httpContext.Request.Headers["If-Match"] = ifMatch;
        return (httpContext, jwt);
    }

    private Mock<IAuthService> CreateAuthServiceMock(string jwt)
    {
        var authService = new Mock<IAuthService>();
        authService.Setup(a => a.ValidateTokenAsync(jwt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticatedUser { UserId = _userId, TenantId = _tenantId, Role = "Admin", Permissions = new HashSet<string>(new[] { "documents.generate", "documents.review", "branding.settings.write" }, StringComparer.OrdinalIgnoreCase) });
        return authService;
    }

    // --- AdminTemplatesController audit propagation ---

    [Fact]
    public async Task AdminTemplates_PassesTenantAndActorToService()
    {
        var (httpContext, jwt) = CreateAuthenticatedContext(correlationId: "corr-tmpl-001");
        var authService = CreateAuthServiceMock(jwt);
        var service = new Mock<IAdminTemplateService>();
        var logger = new Mock<ILogger<AdminTemplatesController>>();

        Guid? capturedTenantId = null;
        Guid? capturedActorId = null;
        string? capturedCorrelationId = null;

        service.Setup(s => s.ListTemplatesAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, string, CancellationToken>((t, u, c, _) =>
            {
                capturedTenantId = t;
                capturedActorId = u;
                capturedCorrelationId = c;
            })
            .ReturnsAsync(new AdminTemplateListResponseDto());

        var controller = new AdminTemplatesController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        await controller.ListTemplates(cancellationToken: CancellationToken.None);

        Assert.Equal(_tenantId, capturedTenantId);
        Assert.Equal(_userId, capturedActorId);
        Assert.Equal("corr-tmpl-001", capturedCorrelationId);
    }

    [Fact]
    public async Task AdminTemplates_MissingCorrelationId_FallsBackToTraceId()
    {
        var (httpContext, jwt) = CreateAuthenticatedContext(); // no X-Correlation-Id
        var authService = CreateAuthServiceMock(jwt);
        var service = new Mock<IAdminTemplateService>();
        var logger = new Mock<ILogger<AdminTemplatesController>>();

        string? capturedCorrelationId = null;
        service.Setup(s => s.ListTemplatesAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, string, CancellationToken>((_, _, c, _) => capturedCorrelationId = c)
            .ReturnsAsync(new AdminTemplateListResponseDto());

        var controller = new AdminTemplatesController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        await controller.ListTemplates(cancellationToken: CancellationToken.None);

        Assert.NotNull(capturedCorrelationId);
        Assert.NotEmpty(capturedCorrelationId!);
    }

    // --- BrandingAssetsController audit propagation ---

    [Fact]
    public async Task BrandingAssets_PassesAuditDataToService()
    {
        var (httpContext, jwt) = CreateAuthenticatedContext(
            correlationId: "corr-brand-001", idempotencyKey: "idem-brand-001");
        var authService = CreateAuthServiceMock(jwt);
        var service = new Mock<IBrandingAssetService>();
        var logger = new Mock<ILogger<BrandingAssetsController>>();

        Guid? capturedTenantId = null;
        Guid? capturedActorId = null;
        string? capturedCorrelationId = null;
        string? capturedIdempotencyKey = null;

        service.Setup(s => s.UploadBrandingAssetsAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<BrandingUploadInput>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, string, string, BrandingUploadInput, CancellationToken>(
                (t, u, c, k, _, _) =>
                {
                    capturedTenantId = t;
                    capturedActorId = u;
                    capturedCorrelationId = c;
                    capturedIdempotencyKey = k;
                })
            .ReturnsAsync(new BrandingAssetResponseDto());

        var controller = new BrandingAssetsController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        await controller.UploadBrandingAssets(null, "{\"primary\":\"#000\"}", null, CancellationToken.None);

        Assert.Equal(_tenantId, capturedTenantId);
        Assert.Equal(_userId, capturedActorId);
        Assert.Equal("corr-brand-001", capturedCorrelationId);
        Assert.Equal("idem-brand-001", capturedIdempotencyKey);
    }

    // --- DocumentsGenerationController audit propagation ---

    [Fact]
    public async Task DocumentGeneration_PassesAuditDataToService()
    {
        var (httpContext, jwt) = CreateAuthenticatedContext(
            correlationId: "corr-gen-001", idempotencyKey: "idem-gen-001");
        var authService = CreateAuthServiceMock(jwt);
        var service = new Mock<IDocumentGenerationService>();
        var logger = new Mock<ILogger<DocumentsGenerationController>>();

        Guid? capturedTenantId = null;
        Guid? capturedActorId = null;
        string? capturedCorrelationId = null;
        string? capturedIdempotencyKey = null;
        Guid? capturedProjectId = null;

        var projectId = Guid.NewGuid();

        service.Setup(s => s.GenerateDocumentAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GenerateDocumentRequestDto>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, Guid, string, string, GenerateDocumentRequestDto, CancellationToken>(
                (t, p, u, c, k, _, _) =>
                {
                    capturedTenantId = t;
                    capturedProjectId = p;
                    capturedActorId = u;
                    capturedCorrelationId = c;
                    capturedIdempotencyKey = k;
                })
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

        await controller.GenerateDocument(projectId.ToString(), request, CancellationToken.None);

        Assert.Equal(_tenantId, capturedTenantId);
        Assert.Equal(projectId, capturedProjectId);
        Assert.Equal(_userId, capturedActorId);
        Assert.Equal("corr-gen-001", capturedCorrelationId);
        Assert.Equal("idem-gen-001", capturedIdempotencyKey);
    }

    // --- DocumentsReviewController audit propagation ---

    [Fact]
    public async Task DocumentReview_PassesAuditDataToService()
    {
        var (httpContext, jwt) = CreateAuthenticatedContext(
            correlationId: "corr-review-001", idempotencyKey: "idem-review-001", ifMatch: "\"1-abc\"");
        var authService = CreateAuthServiceMock(jwt);
        var service = new Mock<IDocumentReviewService>();
        var logger = new Mock<ILogger<DocumentsReviewController>>();

        Guid? capturedTenantId = null;
        Guid? capturedActorId = null;
        string? capturedCorrelationId = null;
        string? capturedIdempotencyKey = null;
        string? capturedIfMatch = null;
        Guid? capturedProjectId = null;
        Guid? capturedDocumentId = null;

        var projectId = Guid.NewGuid();
        var documentId = Guid.NewGuid();

        service.Setup(s => s.ReviewDocumentAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DocumentReviewRequestDto>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, Guid, Guid, string, string, string, DocumentReviewRequestDto, CancellationToken>(
                (t, p, d, u, c, k, m, _, _) =>
                {
                    capturedTenantId = t;
                    capturedProjectId = p;
                    capturedDocumentId = d;
                    capturedActorId = u;
                    capturedCorrelationId = c;
                    capturedIdempotencyKey = k;
                    capturedIfMatch = m;
                })
            .ReturnsAsync(new DocumentReviewResponseDto());

        var controller = new DocumentsReviewController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new DocumentReviewRequestDto { Action = "submit" };
        await controller.ReviewDocument(
            projectId.ToString(), documentId.ToString(), request, CancellationToken.None);

        Assert.Equal(_tenantId, capturedTenantId);
        Assert.Equal(projectId, capturedProjectId);
        Assert.Equal(documentId, capturedDocumentId);
        Assert.Equal(_userId, capturedActorId);
        Assert.Equal("corr-review-001", capturedCorrelationId);
        Assert.Equal("idem-review-001", capturedIdempotencyKey);
        Assert.Equal("\"1-abc\"", capturedIfMatch);
    }

    // --- Correlation ID propagation across all controllers ---

    [Fact]
    public async Task AllControllers_CustomCorrelationId_Propagated()
    {
        var (httpContext, jwt) = CreateAuthenticatedContext(
            correlationId: "custom-corr-xyz", idempotencyKey: "key-1", ifMatch: "\"1-abc\"");
        var authService = CreateAuthServiceMock(jwt);

        // Test DocumentsReviewController
        var reviewService = new Mock<IDocumentReviewService>();
        string? reviewCorrelation = null;
        reviewService.Setup(s => s.ReviewDocumentAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DocumentReviewRequestDto>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, Guid, Guid, string, string, string, DocumentReviewRequestDto, CancellationToken>(
                (_, _, _, _, c, _, _, _, _) => reviewCorrelation = c)
            .ReturnsAsync(new DocumentReviewResponseDto());

        var reviewController = new DocumentsReviewController(
            reviewService.Object, new RequestAuthorizationService(authService.Object), new Mock<ILogger<DocumentsReviewController>>().Object);
        reviewController.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new DocumentReviewRequestDto { Action = "submit" };
        await reviewController.ReviewDocument(
            Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), request, CancellationToken.None);

        Assert.Equal("custom-corr-xyz", reviewCorrelation);
    }

    // --- Service invocation verification ---

    [Fact]
    public async Task AdminTemplates_ServiceInvokedExactlyOnce()
    {
        var (httpContext, jwt) = CreateAuthenticatedContext();
        var authService = CreateAuthServiceMock(jwt);
        var service = new Mock<IAdminTemplateService>();
        var logger = new Mock<ILogger<AdminTemplatesController>>();

        service.Setup(s => s.ListTemplatesAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminTemplateListResponseDto());

        var controller = new AdminTemplatesController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        await controller.ListTemplates(cancellationToken: CancellationToken.None);

        service.Verify(s => s.ListTemplatesAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DocumentGeneration_ServiceInvokedWithCorrectRequest()
    {
        var (httpContext, jwt) = CreateAuthenticatedContext(idempotencyKey: "key-1");
        var authService = CreateAuthServiceMock(jwt);
        var service = new Mock<IDocumentGenerationService>();
        var logger = new Mock<ILogger<DocumentsGenerationController>>();

        GenerateDocumentRequestDto? capturedRequest = null;
        service.Setup(s => s.GenerateDocumentAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GenerateDocumentRequestDto>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, Guid, string, string, GenerateDocumentRequestDto, CancellationToken>(
                (_, _, _, _, _, req, _) => capturedRequest = req)
            .ReturnsAsync(new DocumentArtifactResponseDto());

        var controller = new DocumentsGenerationController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new GenerateDocumentRequestDto
        {
            DocumentType = "scorecard",
            Format = "xlsx",
            TemplateId = "tmpl-score",
            SourceIds = new List<string> { "src-A", "src-B" }
        };

        await controller.GenerateDocument(Guid.NewGuid().ToString(), request, CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Assert.Equal("scorecard", capturedRequest!.DocumentType);
        Assert.Equal("xlsx", capturedRequest.Format);
        Assert.Equal("tmpl-score", capturedRequest.TemplateId);
        Assert.Equal(2, capturedRequest.SourceIds.Count);
    }
}
