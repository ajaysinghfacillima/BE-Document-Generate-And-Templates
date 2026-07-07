// TR: LLD-0021 | ORIGIN: MID_LEVEL_DESIGN-0021
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
/// Tests optimistic concurrency enforcement for document review using If-Match and ETag.
/// Validates that If-Match header is required, ETag mismatch is detected,
/// and successful reviews return updated ETags.
/// </summary>
public class OptimisticConcurrencyTests
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

    private (DocumentsReviewController controller, Mock<IDocumentReviewService> service) CreateController(
        string? ifMatch, string idempotencyKey = "idem-key-1")
    {
        var jwt = CreateJwt(_userId, _tenantId, "Admin");
        var authService = new Mock<IAuthService>();
        authService.Setup(a => a.ValidateTokenAsync(jwt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticatedUser
            {
                UserId = _userId,
                TenantId = _tenantId,
                Role = "Admin",
                Permissions = new HashSet<string>(new[] { "documents.generate", "documents.review", "branding.settings.write" }, StringComparer.OrdinalIgnoreCase)
            });

        var service = new Mock<IDocumentReviewService>();
        var logger = new Mock<ILogger<DocumentsReviewController>>();
        var controller = new DocumentsReviewController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = $"Bearer {jwt}";
        httpContext.Request.Headers["X-Tenant-Id"] = _tenantId.ToString();
        httpContext.Request.Headers["Idempotency-Key"] = idempotencyKey;
        if (ifMatch != null)
            httpContext.Request.Headers["If-Match"] = ifMatch;

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return (controller, service);
    }

    [Fact]
    public async Task DocumentReview_MissingIfMatch_Returns400()
    {
        var (controller, _) = CreateController(ifMatch: null);

        var request = new DocumentReviewRequestDto { Action = "submit" };
        var result = await controller.ReviewDocument(
            Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), request, CancellationToken.None);

        var objectResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Contains("If-Match", error.Message);
    }

    [Fact]
    public async Task DocumentReview_EmptyIfMatch_Returns400()
    {
        var (controller, _) = CreateController(ifMatch: "");

        var request = new DocumentReviewRequestDto { Action = "submit" };
        var result = await controller.ReviewDocument(
            Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), request, CancellationToken.None);

        var objectResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Contains("If-Match", error.Message);
    }

    [Fact]
    public async Task DocumentReview_ETagMismatch_Returns409()
    {
        var (controller, service) = CreateController(ifMatch: "\"1-old\"");

        service.Setup(s => s.ReviewDocumentAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DocumentReviewRequestDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("ETAG_MISMATCH:The document has been modified by another process."));

        var request = new DocumentReviewRequestDto { Action = "approve" };
        var result = await controller.ReviewDocument(
            Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), request, CancellationToken.None);

        var objectResult = Assert.IsType<ConflictObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Equal("ETAG_MISMATCH", error.Code);
    }

    [Fact]
    public async Task DocumentReview_ETagMismatch_MessageStripsPrefix()
    {
        var (controller, service) = CreateController(ifMatch: "\"1-old\"");

        service.Setup(s => s.ReviewDocumentAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DocumentReviewRequestDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("ETAG_MISMATCH:The document has been modified."));

        var request = new DocumentReviewRequestDto { Action = "approve" };
        var result = await controller.ReviewDocument(
            Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), request, CancellationToken.None);

        var objectResult = Assert.IsType<ConflictObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Equal("The document has been modified.", error.Message);
    }

    [Fact]
    public async Task DocumentReview_ValidIfMatch_PassedToService()
    {
        var (controller, service) = CreateController(ifMatch: "\"3-xyz\"");

        string? capturedEtag = null;
        service.Setup(s => s.ReviewDocumentAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DocumentReviewRequestDto>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, Guid, Guid, string, string, string, DocumentReviewRequestDto, CancellationToken>(
                (_, _, _, _, _, _, etag, _, _) => capturedEtag = etag)
            .ReturnsAsync(new DocumentReviewResponseDto { Etag = "\"4-new\"" });

        var request = new DocumentReviewRequestDto { Action = "submit" };
        await controller.ReviewDocument(
            Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), request, CancellationToken.None);

        Assert.Equal("\"3-xyz\"", capturedEtag);
    }

    [Fact]
    public async Task DocumentReview_SuccessfulReview_ReturnsUpdatedEtag()
    {
        var (controller, service) = CreateController(ifMatch: "\"1-abc\"");

        service.Setup(s => s.ReviewDocumentAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DocumentReviewRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentReviewResponseDto
            {
                DocumentId = Guid.NewGuid().ToString(),
                ReviewStatus = "approved",
                Etag = "\"2-def\""
            });

        var request = new DocumentReviewRequestDto { Action = "approve" };
        var result = await controller.ReviewDocument(
            Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), request, CancellationToken.None);

        var objectResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<DocumentReviewResponseDto>(objectResult.Value);
        Assert.Equal("\"2-def\"", response.Etag);
    }

    [Fact]
    public async Task DocumentReview_WhitespaceIfMatch_Returns400()
    {
        var (controller, _) = CreateController(ifMatch: "   ");

        var request = new DocumentReviewRequestDto { Action = "submit" };
        var result = await controller.ReviewDocument(
            Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), request, CancellationToken.None);

        var objectResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objectResult.Value);
        Assert.Contains("If-Match", error.Message);
    }

    // If-Match is only required for document review, not other POST APIs
    [Fact]
    public async Task BrandingAssets_NoIfMatch_Succeeds()
    {
        var jwt = CreateJwt(_userId, _tenantId, "Admin");
        var authService = new Mock<IAuthService>();
        authService.Setup(a => a.ValidateTokenAsync(jwt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticatedUser { UserId = _userId, TenantId = _tenantId, Role = "Admin" });

        var service = new Mock<IBrandingAssetService>();
        service.Setup(s => s.UploadBrandingAssetsAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<BrandingUploadInput>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BrandingAssetResponseDto());

        var logger = new Mock<ILogger<BrandingAssetsController>>();
        var controller = new BrandingAssetsController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = $"Bearer {jwt}";
        httpContext.Request.Headers["X-Tenant-Id"] = _tenantId.ToString();
        httpContext.Request.Headers["Idempotency-Key"] = "key-1";
        // No If-Match header - should be fine for branding
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.UploadBrandingAssets(null, "{\"primary\":\"#000\"}", null, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, objectResult.StatusCode);
    }

    [Fact]
    public async Task DocumentGeneration_NoIfMatch_Succeeds()
    {
        var jwt = CreateJwt(_userId, _tenantId, "Admin");
        var authService = new Mock<IAuthService>();
        authService.Setup(a => a.ValidateTokenAsync(jwt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticatedUser { UserId = _userId, TenantId = _tenantId, Role = "Admin" });

        var service = new Mock<IDocumentGenerationService>();
        service.Setup(s => s.GenerateDocumentAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GenerateDocumentRequestDto>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocumentArtifactResponseDto());

        var logger = new Mock<ILogger<DocumentsGenerationController>>();
        var controller = new DocumentsGenerationController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = $"Bearer {jwt}";
        httpContext.Request.Headers["X-Tenant-Id"] = _tenantId.ToString();
        httpContext.Request.Headers["Idempotency-Key"] = "key-generation-001";
        // No If-Match header - should be fine for generation
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var request = new GenerateDocumentRequestDto
        {
            DocumentType = "narrative",
            Format = "pdf",
            TemplateId = "tmpl-001",
            SourceIds = new List<string> { "src-001" }
        };

        var result = await controller.GenerateDocument(Guid.NewGuid().ToString(), request, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, objectResult.StatusCode);
    }
}
