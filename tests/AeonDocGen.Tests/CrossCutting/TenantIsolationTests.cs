// TR: LLD-0004 | ORIGIN: MID_LEVEL_DESIGN-0004
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AeonDocGen.Core.DTOs;
using AeonDocGen.Core.Interfaces;
using AeonDocGen.Core.Validators;
using AeonDocGen.Api.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace AeonDocGen.Tests.CrossCutting;

/// <summary>
/// Tests tenant isolation enforcement across all scoped APIs.
/// Validates that X-Tenant-Id matches the authenticated principal's tenant scope
/// and that cross-tenant access is consistently denied.
/// </summary>
public class TenantIsolationTests
{
    private readonly Guid _tenantA = Guid.Parse("00000000-0000-0000-0000-00000000000A");
    private readonly Guid _tenantB = Guid.Parse("00000000-0000-0000-0000-00000000000B");
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

    // Validator-level tenant isolation
    [Fact]
    public void HeaderValidator_TenantIsolation_SameTenant_Allowed()
    {
        var error = HeaderValidator.ValidateTenantIsolation(_tenantA, _tenantA, "trace-1");
        Assert.Null(error);
    }

    [Fact]
    public void HeaderValidator_TenantIsolation_DifferentTenants_Blocked()
    {
        var error = HeaderValidator.ValidateTenantIsolation(_tenantA, _tenantB, "trace-1");
        Assert.NotNull(error);
        Assert.Equal("FORBIDDEN", error!.Code);
    }

    // AdminTemplatesController - tenant isolation
    [Fact]
    public async Task AdminTemplatesController_TenantMismatch_Returns403()
    {
        var authService = new Mock<IAuthService>();
        var service = new Mock<IAdminTemplateService>();
        var logger = new Mock<ILogger<AdminTemplatesController>>();
        var controller = new AdminTemplatesController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);

        var jwt = CreateJwt(_userId, _tenantA, "Admin");
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = $"Bearer {jwt}";
        httpContext.Request.Headers["X-Tenant-Id"] = _tenantB.ToString();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        authService.Setup(a => a.ValidateTokenAsync(jwt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticatedUser { UserId = _userId, TenantId = _tenantA, Role = "Admin" });

        var result = await controller.ListTemplates(cancellationToken: CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    // BrandingAssetsController - tenant isolation
    [Fact]
    public async Task BrandingAssetsController_TenantMismatch_Returns403()
    {
        var authService = new Mock<IAuthService>();
        var service = new Mock<IBrandingAssetService>();
        var logger = new Mock<ILogger<BrandingAssetsController>>();
        var controller = new BrandingAssetsController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);

        var jwt = CreateJwt(_userId, _tenantA, "Admin");
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = $"Bearer {jwt}";
        httpContext.Request.Headers["X-Tenant-Id"] = _tenantB.ToString();
        httpContext.Request.Headers["Idempotency-Key"] = "key-1";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        authService.Setup(a => a.ValidateTokenAsync(jwt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticatedUser { UserId = _userId, TenantId = _tenantA, Role = "Admin" });

        var result = await controller.UploadBrandingAssets(null, null, null, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    // DocumentsGenerationController - tenant isolation
    [Fact]
    public async Task DocumentsGenerationController_TenantMismatch_Returns403()
    {
        var authService = new Mock<IAuthService>();
        var service = new Mock<IDocumentGenerationService>();
        var logger = new Mock<ILogger<DocumentsGenerationController>>();
        var controller = new DocumentsGenerationController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);

        var jwt = CreateJwt(_userId, _tenantA, "Admin");
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = $"Bearer {jwt}";
        httpContext.Request.Headers["X-Tenant-Id"] = _tenantB.ToString();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        authService.Setup(a => a.ValidateTokenAsync(jwt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticatedUser { UserId = _userId, TenantId = _tenantA, Role = "Admin" });

        var request = new GenerateDocumentRequestDto
        {
            DocumentType = "narrative",
            Format = "pdf",
            TemplateId = "tmpl-001",
            SourceIds = new List<string> { "src-001" }
        };

        var result = await controller.GenerateDocument(Guid.NewGuid().ToString(), request, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    // DocumentsReviewController - tenant isolation
    [Fact]
    public async Task DocumentsReviewController_TenantMismatch_Returns403()
    {
        var authService = new Mock<IAuthService>();
        var service = new Mock<IDocumentReviewService>();
        var logger = new Mock<ILogger<DocumentsReviewController>>();
        var controller = new DocumentsReviewController(service.Object, new RequestAuthorizationService(authService.Object), logger.Object);

        var jwt = CreateJwt(_userId, _tenantA, "Admin");
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = $"Bearer {jwt}";
        httpContext.Request.Headers["X-Tenant-Id"] = _tenantB.ToString();
        httpContext.Request.Headers["Idempotency-Key"] = "key-1";
        httpContext.Request.Headers["If-Match"] = "\"1-abc\"";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        authService.Setup(a => a.ValidateTokenAsync(jwt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticatedUser { UserId = _userId, TenantId = _tenantA, Role = "Admin" });

        var request = new DocumentReviewRequestDto { Action = "submit" };
        var result = await controller.ReviewDocument(Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), request, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    // Ensure tenant scope error uses StandardError format
    [Fact]
    public void TenantIsolation_ErrorFormat_IsStandardError()
    {
        var error = HeaderValidator.ValidateTenantIsolation(_tenantA, _tenantB, "trace-123");
        Assert.NotNull(error);
        Assert.NotNull(error!.TraceId);
        Assert.NotNull(error.Code);
        Assert.NotNull(error.Message);
        Assert.Equal("trace-123", error.TraceId);
    }

    // Role-based tenant isolation for document review
    [Fact]
    public void ExternalAuditor_CannotReview_Documents()
    {
        var allowed = new[] { "Sustainability Consultant", "Admin", "Owner", "PMC" };
        var error = HeaderValidator.ValidateRole("External Auditor", allowed, "trace-1", "FORBIDDEN_DOCUMENT_REVIEW");
        Assert.NotNull(error);
        Assert.Equal("FORBIDDEN_DOCUMENT_REVIEW", error!.Code);
    }
}
