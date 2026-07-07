using AeonDocGen.Api.Controllers;
using AeonDocGen.Api.Policies;
using AeonDocGen.Core.DTOs;
using AeonDocGen.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace AeonDocGen.Tests;

public class DocumentsGenerationControllerTests
{
    private readonly Mock<IDocumentGenerationService> _serviceMock;
    private readonly Mock<IAuthService> _authServiceMock;
    private readonly DocumentsGenerationController _controller;
    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private readonly Guid _userId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private readonly Guid _projectId = Guid.Parse("00000000-0000-0000-0000-000000000003");

    public DocumentsGenerationControllerTests()
    {
        _serviceMock = new Mock<IDocumentGenerationService>();
        _authServiceMock = new Mock<IAuthService>();
        _controller = new DocumentsGenerationController(
            _serviceMock.Object,
            new RequestAuthorizationService(_authServiceMock.Object),
            new Mock<ILogger<DocumentsGenerationController>>().Object);

        var httpContext = new DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    private void SetupAuth()
    {
        _controller.HttpContext.Request.Headers["Authorization"] = "Bearer valid-token";
        _controller.HttpContext.Request.Headers["X-Tenant-Id"] = _tenantId.ToString();
        _controller.HttpContext.Request.Headers["Idempotency-Key"] = "key-default";
        _authServiceMock.Setup(s => s.ValidateTokenAsync("valid-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticatedUser
            {
                UserId = _userId,
                TenantId = _tenantId,
                Role = "Sustainability Consultant",
                Permissions = new HashSet<string>(new[] { "documents.generate" }, StringComparer.OrdinalIgnoreCase),
                ProjectScopeIds = new HashSet<Guid> { _projectId }
            });
    }

    [Fact]
    public async Task GenerateDocument_MissingAuth_Returns401()
    {
        var request = new GenerateDocumentRequestDto();
        var result = await _controller.GenerateDocument(_projectId.ToString(), request, CancellationToken.None);

        var objResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objResult.Value);
        Assert.Equal("UNAUTHENTICATED", error.Code);
    }

    [Fact]
    public async Task GenerateDocument_InvalidToken_Returns401()
    {
        _controller.HttpContext.Request.Headers["Authorization"] = "Bearer bad-token";
        _controller.HttpContext.Request.Headers["X-Tenant-Id"] = _tenantId.ToString();
        _authServiceMock.Setup(s => s.ValidateTokenAsync("bad-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuthenticatedUser?)null);

        var result = await _controller.GenerateDocument(_projectId.ToString(), new GenerateDocumentRequestDto(), CancellationToken.None);

        var objResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objResult.Value);
        Assert.Equal("UNAUTHENTICATED", error.Code);
    }

    [Fact]
    public async Task GenerateDocument_MissingTenantHeader_Returns400()
    {
        _controller.HttpContext.Request.Headers["Authorization"] = "Bearer valid-token";
        _authServiceMock.Setup(s => s.ValidateTokenAsync("valid-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticatedUser { UserId = _userId, TenantId = _tenantId, Role = "Admin" });

        var result = await _controller.GenerateDocument(_projectId.ToString(), new GenerateDocumentRequestDto(), CancellationToken.None);

        var objResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objResult.Value);
        Assert.Equal("INVALID_REQUEST", error.Code);
    }

    [Fact]
    public async Task GenerateDocument_TenantMismatch_Returns403()
    {
        _controller.HttpContext.Request.Headers["Authorization"] = "Bearer valid-token";
        _controller.HttpContext.Request.Headers["X-Tenant-Id"] = Guid.NewGuid().ToString();
        _authServiceMock.Setup(s => s.ValidateTokenAsync("valid-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticatedUser { UserId = _userId, TenantId = _tenantId, Role = "Admin" });

        var result = await _controller.GenerateDocument(_projectId.ToString(), new GenerateDocumentRequestDto(), CancellationToken.None);

        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, objResult.StatusCode);
    }

    [Fact]
    public async Task GenerateDocument_InvalidProjectId_Returns400()
    {
        SetupAuth();
        var result = await _controller.GenerateDocument("not-a-guid", new GenerateDocumentRequestDto(), CancellationToken.None);

        var objResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objResult.Value);
        Assert.Equal("INVALID_REQUEST", error.Code);
    }

    [Fact]
    public async Task GenerateDocument_EmptyGuidProjectId_Returns400()
    {
        SetupAuth();
        var result = await _controller.GenerateDocument(Guid.Empty.ToString(), new GenerateDocumentRequestDto(), CancellationToken.None);

        var objResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objResult.Value);
        Assert.Equal("INVALID_REQUEST", error.Code);
        Assert.Equal("projectId must be a non-empty valid identifier.", error.Message);
    }

    [Fact]
    public async Task GenerateDocument_Success_Returns201()
    {
        SetupAuth();
        var expectedResponse = new DocumentArtifactResponseDto
        {
            Id = "doc-001",
            DocumentType = "narrative",
            Format = "pdf",
            ReviewStatus = "draft"
        };

        _serviceMock.Setup(s => s.GenerateDocumentAsync(
            _tenantId, _projectId, _userId, It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<GenerateDocumentRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var request = new GenerateDocumentRequestDto
        {
            DocumentType = "narrative",
            Format = "pdf",
            TemplateId = Guid.NewGuid().ToString(),
            IncludeBranding = false,
            SourceIds = new List<string> { Guid.NewGuid().ToString() }
        };

        var result = await _controller.GenerateDocument(_projectId.ToString(), request, CancellationToken.None);

        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, objResult.StatusCode);
        var response = Assert.IsType<DocumentArtifactResponseDto>(objResult.Value);
        Assert.Equal("doc-001", response.Id);
    }

    [Fact]
    public async Task GenerateDocument_ValidationError_Returns400()
    {
        SetupAuth();
        _serviceMock.Setup(s => s.GenerateDocumentAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<GenerateDocumentRequestDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("documentType is invalid"));

        var result = await _controller.GenerateDocument(_projectId.ToString(), new GenerateDocumentRequestDto(), CancellationToken.None);

        var objResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objResult.Value);
        Assert.Equal("INVALID_REQUEST", error.Code);
    }

    [Fact]
    public async Task GenerateDocument_NotFound_Returns404()
    {
        SetupAuth();
        _serviceMock.Setup(s => s.GenerateDocumentAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<GenerateDocumentRequestDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Project not found"));

        var result = await _controller.GenerateDocument(_projectId.ToString(), new GenerateDocumentRequestDto(), CancellationToken.None);

        var objResult = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objResult.Value);
        Assert.Equal("NOT_FOUND", error.Code);
    }

    [Fact]
    public async Task GenerateDocument_IdempotencyConflict_Returns409()
    {
        SetupAuth();
        _controller.HttpContext.Request.Headers["Idempotency-Key"] = "key-1";
        _serviceMock.Setup(s => s.GenerateDocumentAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<GenerateDocumentRequestDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Idempotency-Key has been used with a different payload."));

        var result = await _controller.GenerateDocument(_projectId.ToString(), new GenerateDocumentRequestDto(), CancellationToken.None);

        var objResult = Assert.IsType<ConflictObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objResult.Value);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED_WITH_DIFFERENT_PAYLOAD", error.Code);
    }

    [Fact]
    public async Task GenerateDocument_InternalError_Returns500()
    {
        SetupAuth();
        _serviceMock.Setup(s => s.GenerateDocumentAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<GenerateDocumentRequestDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Unexpected error"));

        var result = await _controller.GenerateDocument(_projectId.ToString(), new GenerateDocumentRequestDto(), CancellationToken.None);

        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objResult.StatusCode);
    }

    [Fact]
    public async Task GenerateDocument_MissingIdempotencyKey_Returns400()
    {
        SetupAuth();
        _controller.HttpContext.Request.Headers.Remove("Idempotency-Key");

        var request = new GenerateDocumentRequestDto
        {
            DocumentType = "narrative",
            Format = "pdf",
            TemplateId = Guid.NewGuid().ToString(),
            SourceIds = new List<string> { Guid.NewGuid().ToString() }
        };

        var result = await _controller.GenerateDocument(_projectId.ToString(), request, CancellationToken.None);

        var objResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objResult.Value);
        Assert.Equal("INVALID_REQUEST_BODY", error.Code);
        Assert.Contains("Idempotency-Key", error.Message);
    }
}
