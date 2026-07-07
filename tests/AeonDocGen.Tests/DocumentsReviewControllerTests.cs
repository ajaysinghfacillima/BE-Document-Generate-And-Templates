using AeonDocGen.Api.Controllers;
using AeonDocGen.Api.Policies;
using AeonDocGen.Core.DTOs;
using AeonDocGen.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace AeonDocGen.Tests;

public class DocumentsReviewControllerTests
{
    private readonly Mock<IDocumentReviewService> _serviceMock;
    private readonly Mock<IAuthService> _authServiceMock;
    private readonly DocumentsReviewController _controller;
    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private readonly Guid _userId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private readonly Guid _projectId = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private readonly Guid _documentId = Guid.Parse("00000000-0000-0000-0000-000000000006");

    public DocumentsReviewControllerTests()
    {
        _serviceMock = new Mock<IDocumentReviewService>();
        _authServiceMock = new Mock<IAuthService>();
        _controller = new DocumentsReviewController(
            _serviceMock.Object,
            new RequestAuthorizationService(_authServiceMock.Object),
            new Mock<ILogger<DocumentsReviewController>>().Object);

        var httpContext = new DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    private void SetupAuth(string role = "Sustainability Consultant")
    {
        _controller.HttpContext.Request.Headers["Authorization"] = "Bearer valid-token";
        _controller.HttpContext.Request.Headers["X-Tenant-Id"] = _tenantId.ToString();
        _controller.HttpContext.Request.Headers["Idempotency-Key"] = "key-1";
        _controller.HttpContext.Request.Headers["If-Match"] = "\"1-abc\"";
        _authServiceMock.Setup(s => s.ValidateTokenAsync("valid-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticatedUser
            {
                UserId = _userId,
                TenantId = _tenantId,
                Role = role,
                Permissions = new HashSet<string>(new[] { "documents.review" }, StringComparer.OrdinalIgnoreCase),
                ProjectScopeIds = new HashSet<Guid> { _projectId }
            });
    }

    [Fact]
    public async Task ReviewDocument_MissingAuth_Returns401()
    {
        var request = new DocumentReviewRequestDto { Action = "submit" };
        var result = await _controller.ReviewDocument(_projectId.ToString(), _documentId.ToString(), request, CancellationToken.None);

        var objResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objResult.Value);
        Assert.Equal("UNAUTHENTICATED", error.Code);
    }

    [Fact]
    public async Task ReviewDocument_UnauthorizedRole_Returns403()
    {
        SetupAuth("External Auditor");

        var request = new DocumentReviewRequestDto { Action = "submit" };
        var result = await _controller.ReviewDocument(_projectId.ToString(), _documentId.ToString(), request, CancellationToken.None);

        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, objResult.StatusCode);
        var error = Assert.IsType<StandardErrorDto>(objResult.Value);
        Assert.Equal("FORBIDDEN_DOCUMENT_REVIEW", error.Code);
    }

    [Theory]
    [InlineData("Sustainability Consultant")]
    [InlineData("Admin")]
    [InlineData("Owner")]
    [InlineData("PMC")]
    public async Task ReviewDocument_AllowedRoles_Accepted(string role)
    {
        SetupAuth(role);
        var expectedResponse = new DocumentReviewResponseDto
        {
            DocumentId = _documentId.ToString(),
            ProjectId = _projectId.ToString(),
            ReviewStatus = "inReview"
        };

        _serviceMock.Setup(s => s.ReviewDocumentAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<DocumentReviewRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var request = new DocumentReviewRequestDto { Action = "startReview" };
        var result = await _controller.ReviewDocument(_projectId.ToString(), _documentId.ToString(), request, CancellationToken.None);

        var objResult = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<DocumentReviewResponseDto>(objResult.Value);
    }

    [Fact]
    public async Task ReviewDocument_MissingIdempotencyKey_Returns400()
    {
        _controller.HttpContext.Request.Headers["Authorization"] = "Bearer valid-token";
        _controller.HttpContext.Request.Headers["X-Tenant-Id"] = _tenantId.ToString();
        _controller.HttpContext.Request.Headers["If-Match"] = "\"1-abc\"";
        _authServiceMock.Setup(s => s.ValidateTokenAsync("valid-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticatedUser
            {
                UserId = _userId,
                TenantId = _tenantId,
                Role = "Admin",
                Permissions = new HashSet<string>(new[] { "documents.review" }, StringComparer.OrdinalIgnoreCase),
                ProjectScopeIds = new HashSet<Guid> { _projectId }
            });

        var request = new DocumentReviewRequestDto { Action = "submit" };
        var result = await _controller.ReviewDocument(_projectId.ToString(), _documentId.ToString(), request, CancellationToken.None);

        var objResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objResult.Value);
        Assert.Equal("INVALID_REQUEST_BODY", error.Code);
    }

    [Fact]
    public async Task ReviewDocument_MissingIfMatch_Returns400()
    {
        _controller.HttpContext.Request.Headers["Authorization"] = "Bearer valid-token";
        _controller.HttpContext.Request.Headers["X-Tenant-Id"] = _tenantId.ToString();
        _controller.HttpContext.Request.Headers["Idempotency-Key"] = "key-1";
        _authServiceMock.Setup(s => s.ValidateTokenAsync("valid-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticatedUser
            {
                UserId = _userId,
                TenantId = _tenantId,
                Role = "Admin",
                Permissions = new HashSet<string>(new[] { "documents.review" }, StringComparer.OrdinalIgnoreCase),
                ProjectScopeIds = new HashSet<Guid> { _projectId }
            });

        var request = new DocumentReviewRequestDto { Action = "submit" };
        var result = await _controller.ReviewDocument(_projectId.ToString(), _documentId.ToString(), request, CancellationToken.None);

        var objResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objResult.Value);
        Assert.Equal("INVALID_REQUEST_BODY", error.Code);
    }

    [Fact]
    public async Task ReviewDocument_InvalidProjectId_Returns400()
    {
        SetupAuth();
        var request = new DocumentReviewRequestDto { Action = "submit" };
        var result = await _controller.ReviewDocument("not-a-guid", _documentId.ToString(), request, CancellationToken.None);

        var objResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("INVALID_REQUEST", ((StandardErrorDto)objResult.Value!).Code);
    }

    [Fact]
    public async Task ReviewDocument_InvalidDocumentId_Returns400()
    {
        SetupAuth();
        var request = new DocumentReviewRequestDto { Action = "submit" };
        var result = await _controller.ReviewDocument(_projectId.ToString(), "not-a-guid", request, CancellationToken.None);

        var objResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("INVALID_REQUEST", ((StandardErrorDto)objResult.Value!).Code);
    }

    [Fact]
    public async Task ReviewDocument_InvalidReviewAction_Returns400()
    {
        SetupAuth();
        _serviceMock.Setup(s => s.ReviewDocumentAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<DocumentReviewRequestDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("INVALID_REVIEW_ACTION:action is invalid"));

        var request = new DocumentReviewRequestDto { Action = "invalid" };
        var result = await _controller.ReviewDocument(_projectId.ToString(), _documentId.ToString(), request, CancellationToken.None);

        var objResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objResult.Value);
        Assert.Equal("INVALID_REVIEW_ACTION", error.Code);
    }

    [Fact]
    public async Task ReviewDocument_ProjectNotFound_Returns404()
    {
        SetupAuth();
        _serviceMock.Setup(s => s.ReviewDocumentAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<DocumentReviewRequestDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("PROJECT_NOT_FOUND:Project not found"));

        var request = new DocumentReviewRequestDto { Action = "submit" };
        var result = await _controller.ReviewDocument(_projectId.ToString(), _documentId.ToString(), request, CancellationToken.None);

        var objResult = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objResult.Value);
        Assert.Equal("PROJECT_NOT_FOUND", error.Code);
    }

    [Fact]
    public async Task ReviewDocument_DocumentNotFound_Returns404()
    {
        SetupAuth();
        _serviceMock.Setup(s => s.ReviewDocumentAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<DocumentReviewRequestDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("DOCUMENT_NOT_FOUND:Document not found"));

        var request = new DocumentReviewRequestDto { Action = "submit" };
        var result = await _controller.ReviewDocument(_projectId.ToString(), _documentId.ToString(), request, CancellationToken.None);

        var objResult = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objResult.Value);
        Assert.Equal("DOCUMENT_NOT_FOUND", error.Code);
    }

    [Fact]
    public async Task ReviewDocument_ETagMismatch_Returns409()
    {
        SetupAuth();
        _serviceMock.Setup(s => s.ReviewDocumentAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<DocumentReviewRequestDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("ETAG_MISMATCH:Stale etag"));

        var request = new DocumentReviewRequestDto { Action = "submit" };
        var result = await _controller.ReviewDocument(_projectId.ToString(), _documentId.ToString(), request, CancellationToken.None);

        var objResult = Assert.IsType<ConflictObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objResult.Value);
        Assert.Equal("ETAG_MISMATCH", error.Code);
    }

    [Fact]
    public async Task ReviewDocument_IdempotencyConflict_Returns409()
    {
        SetupAuth();
        _serviceMock.Setup(s => s.ReviewDocumentAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<DocumentReviewRequestDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Idempotency-Key has been used with a different payload."));

        var request = new DocumentReviewRequestDto { Action = "submit" };
        var result = await _controller.ReviewDocument(_projectId.ToString(), _documentId.ToString(), request, CancellationToken.None);

        var objResult = Assert.IsType<ConflictObjectResult>(result);
        var error = Assert.IsType<StandardErrorDto>(objResult.Value);
        Assert.Equal("IDEMPOTENCY_KEY_REUSED_WITH_DIFFERENT_PAYLOAD", error.Code);
    }

    [Fact]
    public async Task ReviewDocument_InternalError_Returns500()
    {
        SetupAuth();
        _serviceMock.Setup(s => s.ReviewDocumentAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<DocumentReviewRequestDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Unexpected"));

        var request = new DocumentReviewRequestDto { Action = "submit" };
        var result = await _controller.ReviewDocument(_projectId.ToString(), _documentId.ToString(), request, CancellationToken.None);

        var objResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objResult.StatusCode);
        var error = Assert.IsType<StandardErrorDto>(objResult.Value);
        Assert.Equal("DOCUMENT_REVIEW_PROCESSING_FAILED", error.Code);
    }

    [Fact]
    public async Task ReviewDocument_Success_Returns200()
    {
        SetupAuth();
        var expectedResponse = new DocumentReviewResponseDto
        {
            DocumentId = _documentId.ToString(),
            ProjectId = _projectId.ToString(),
            ReviewStatus = "inReview",
            Event = new DocumentReviewEventDto { Action = "startReview", ActorUserId = _userId.ToString() },
            Etag = "\"2-xyz\""
        };

        _serviceMock.Setup(s => s.ReviewDocumentAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<DocumentReviewRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var request = new DocumentReviewRequestDto { Action = "startReview" };
        var result = await _controller.ReviewDocument(_projectId.ToString(), _documentId.ToString(), request, CancellationToken.None);

        var objResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, objResult.StatusCode);
        var response = Assert.IsType<DocumentReviewResponseDto>(objResult.Value);
        Assert.Equal("inReview", response.ReviewStatus);
    }
}
