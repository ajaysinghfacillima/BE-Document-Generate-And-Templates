using System.Data;
using System.Text.Json;
using AeonDocGen.Core.DTOs;
using AeonDocGen.Core.Interfaces;
using AeonDocGen.Core.Models;
using AeonDocGen.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace AeonDocGen.Tests;

public class DocumentReviewServiceTests
{
    private readonly Mock<IProjectRepository> _projectRepoMock;
    private readonly Mock<IDocumentArtifactRepository> _docArtifactRepoMock;
    private readonly Mock<IDocumentReviewEventRepository> _reviewEventRepoMock;
    private readonly Mock<IAuditLogRepository> _auditRepoMock;
    private readonly Mock<IIdempotencyRepository> _idempotencyRepoMock;
    private readonly Mock<IDbConnectionFactory> _connFactoryMock;
    private readonly Mock<IDbConnection> _connectionMock;
    private readonly Mock<IDbTransaction> _transactionMock;
    private readonly DocumentReviewService _service;
    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private readonly Guid _userId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private readonly Guid _projectId = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private readonly Guid _documentId = Guid.Parse("00000000-0000-0000-0000-000000000006");
    private const string ValidEtag = "\"1-abc\"";

    public DocumentReviewServiceTests()
    {
        _projectRepoMock = new Mock<IProjectRepository>();
        _docArtifactRepoMock = new Mock<IDocumentArtifactRepository>();
        _reviewEventRepoMock = new Mock<IDocumentReviewEventRepository>();
        _auditRepoMock = new Mock<IAuditLogRepository>();
        _idempotencyRepoMock = new Mock<IIdempotencyRepository>();
        _connFactoryMock = new Mock<IDbConnectionFactory>();
        _connectionMock = new Mock<IDbConnection>();
        _transactionMock = new Mock<IDbTransaction>();

        _connectionMock.Setup(c => c.BeginTransaction()).Returns(_transactionMock.Object);
        _connFactoryMock.Setup(f => f.CreateConnection()).Returns(_connectionMock.Object);

        _service = new DocumentReviewService(
            _projectRepoMock.Object,
            _docArtifactRepoMock.Object,
            _reviewEventRepoMock.Object,
            _auditRepoMock.Object,
            _idempotencyRepoMock.Object,
            _connFactoryMock.Object,
            new Mock<ILogger<DocumentReviewService>>().Object);
    }

    private DocumentArtifactEntity CreateDraftDocument()
    {
        return new DocumentArtifactEntity
        {
            DocumentArtifactId = _documentId,
            TenantId = _tenantId,
            ProjectId = _projectId,
            DocumentType = "narrative",
            Format = "pdf",
            TemplateId = Guid.NewGuid(),
            TemplateVersion = "1.0",
            ReviewStatus = "draft",
            Etag = ValidEtag,
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private DocumentArtifactEntity CreateInReviewDocument()
    {
        var doc = CreateDraftDocument();
        doc.ReviewStatus = "inReview";
        return doc;
    }

    private void SetupValidScenario(DocumentArtifactEntity? doc = null)
    {
        doc ??= CreateDraftDocument();
        _projectRepoMock.Setup(r => r.ExistsAsync(_projectId, _tenantId, It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _docArtifactRepoMock.Setup(r => r.GetByIdAsync(_documentId, _projectId, _tenantId, It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);
        _docArtifactRepoMock.Setup(r => r.UpdateReviewStatusAsync(It.IsAny<DocumentArtifactEntity>(), ValidEtag, It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    [Fact]
    public async Task ReviewDocument_SubmitAction_RecordsEvent()
    {
        SetupValidScenario();
        var request = new DocumentReviewRequestDto { Action = "submit" };

        var result = await _service.ReviewDocumentAsync(
            _tenantId, _projectId, _documentId, _userId, "corr-1", "key-1", ValidEtag, request);

        Assert.Equal("draft", result.ReviewStatus);
        Assert.Equal("submit", result.Event.Action);
        Assert.Equal(_documentId.ToString(), result.DocumentId);
        Assert.Equal(_projectId.ToString(), result.ProjectId);
    }

    [Fact]
    public async Task ReviewDocument_StartReviewAction_TransitionsDraftToInReview()
    {
        SetupValidScenario();
        var request = new DocumentReviewRequestDto { Action = "startReview" };

        var result = await _service.ReviewDocumentAsync(
            _tenantId, _projectId, _documentId, _userId, "corr-1", "key-1", ValidEtag, request);

        Assert.Equal("inReview", result.ReviewStatus);
        Assert.Equal("startReview", result.Event.Action);
    }

    [Fact]
    public async Task ReviewDocument_ApproveAction_TransitionsInReviewToApproved()
    {
        var doc = CreateInReviewDocument();
        SetupValidScenario(doc);
        var request = new DocumentReviewRequestDto { Action = "approve", Comments = "Looks good." };

        var result = await _service.ReviewDocumentAsync(
            _tenantId, _projectId, _documentId, _userId, "corr-1", "key-1", ValidEtag, request);

        Assert.Equal("approved", result.ReviewStatus);
        Assert.Equal("approve", result.Event.Action);
        Assert.Equal(_userId.ToString(), result.ReviewedByUserId);
        Assert.NotNull(result.ReviewedAt);
    }

    [Fact]
    public async Task ReviewDocument_RejectAction_TransitionsInReviewToRejected()
    {
        var doc = CreateInReviewDocument();
        SetupValidScenario(doc);
        var request = new DocumentReviewRequestDto { Action = "reject", Comments = "Needs changes." };

        var result = await _service.ReviewDocumentAsync(
            _tenantId, _projectId, _documentId, _userId, "corr-1", "key-1", ValidEtag, request);

        Assert.Equal("rejected", result.ReviewStatus);
        Assert.Equal("reject", result.Event.Action);
        Assert.Equal("Needs changes.", result.Event.Comments);
    }

    [Fact]
    public async Task ReviewDocument_ApproveFromDraft_ThrowsArgumentException()
    {
        SetupValidScenario();
        var request = new DocumentReviewRequestDto { Action = "approve" };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ReviewDocumentAsync(_tenantId, _projectId, _documentId, _userId, "corr-1", "key-1", ValidEtag, request));
        Assert.Contains("INVALID_REVIEW_ACTION", ex.Message);
    }

    [Fact]
    public async Task ReviewDocument_RejectFromDraft_ThrowsArgumentException()
    {
        SetupValidScenario();
        var request = new DocumentReviewRequestDto { Action = "reject" };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ReviewDocumentAsync(_tenantId, _projectId, _documentId, _userId, "corr-1", "key-1", ValidEtag, request));
        Assert.Contains("INVALID_REVIEW_ACTION", ex.Message);
    }

    [Fact]
    public async Task ReviewDocument_InvalidAction_ThrowsArgumentException()
    {
        var request = new DocumentReviewRequestDto { Action = "invalid" };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ReviewDocumentAsync(_tenantId, _projectId, _documentId, _userId, "corr-1", "key-1", ValidEtag, request));
        Assert.Contains("INVALID_REVIEW_ACTION", ex.Message);
    }

    [Fact]
    public async Task ReviewDocument_EmptyAction_ThrowsArgumentException()
    {
        var request = new DocumentReviewRequestDto { Action = "" };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ReviewDocumentAsync(_tenantId, _projectId, _documentId, _userId, "corr-1", "key-1", ValidEtag, request));
        Assert.Contains("INVALID_REQUEST_BODY", ex.Message);
    }

    [Fact]
    public async Task ReviewDocument_CommentsTooLong_ThrowsArgumentException()
    {
        var request = new DocumentReviewRequestDto { Action = "submit", Comments = new string('A', 2001) };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ReviewDocumentAsync(_tenantId, _projectId, _documentId, _userId, "corr-1", "key-1", ValidEtag, request));
        Assert.Contains("INVALID_REQUEST_BODY", ex.Message);
    }

    [Fact]
    public async Task ReviewDocument_ProjectNotFound_ThrowsKeyNotFoundException()
    {
        _projectRepoMock.Setup(r => r.ExistsAsync(_projectId, _tenantId, It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var request = new DocumentReviewRequestDto { Action = "submit" };

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.ReviewDocumentAsync(_tenantId, _projectId, _documentId, _userId, "corr-1", "key-1", ValidEtag, request));
        Assert.Contains("PROJECT_NOT_FOUND", ex.Message);
    }

    [Fact]
    public async Task ReviewDocument_DocumentNotFound_ThrowsKeyNotFoundException()
    {
        _projectRepoMock.Setup(r => r.ExistsAsync(_projectId, _tenantId, It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _docArtifactRepoMock.Setup(r => r.GetByIdAsync(_documentId, _projectId, _tenantId, It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DocumentArtifactEntity?)null);
        var request = new DocumentReviewRequestDto { Action = "submit" };

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.ReviewDocumentAsync(_tenantId, _projectId, _documentId, _userId, "corr-1", "key-1", ValidEtag, request));
        Assert.Contains("DOCUMENT_NOT_FOUND", ex.Message);
    }

    [Fact]
    public async Task ReviewDocument_ETagMismatch_ThrowsInvalidOperationException()
    {
        _projectRepoMock.Setup(r => r.ExistsAsync(_projectId, _tenantId, It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _docArtifactRepoMock.Setup(r => r.GetByIdAsync(_documentId, _projectId, _tenantId, It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDraftDocument());
        var request = new DocumentReviewRequestDto { Action = "submit" };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ReviewDocumentAsync(_tenantId, _projectId, _documentId, _userId, "corr-1", "key-1", "wrong-etag", request));
        Assert.Contains("ETAG_MISMATCH", ex.Message);
    }

    [Fact]
    public async Task ReviewDocument_IdempotentReplay_ReturnsCachedResponse()
    {
        var cachedResponse = new DocumentReviewResponseDto
        {
            DocumentId = _documentId.ToString(),
            ProjectId = _projectId.ToString(),
            ReviewStatus = "inReview"
        };

        var request = new DocumentReviewRequestDto { Action = "startReview" };
        var payload = $"{_projectId}|{_documentId}|{request.Action}|";
        var hashBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payload));
        var requestHash = Convert.ToHexStringLower(hashBytes);

        _idempotencyRepoMock.Setup(r => r.GetByKeyAsync("key-replay", _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdempotencyRecordEntity
            {
                IdempotencyKey = "key-replay",
                TenantId = _tenantId,
                RequestHash = requestHash,
                ResponseJson = JsonSerializer.Serialize(cachedResponse),
                StatusCode = 200
            });

        var result = await _service.ReviewDocumentAsync(
            _tenantId, _projectId, _documentId, _userId, "corr-1", "key-replay", ValidEtag, request);

        Assert.Equal("inReview", result.ReviewStatus);
    }

    [Fact]
    public async Task ReviewDocument_IdempotencyKeyDifferentPayload_ThrowsInvalidOperationException()
    {
        _idempotencyRepoMock.Setup(r => r.GetByKeyAsync("key-dup", _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdempotencyRecordEntity
            {
                IdempotencyKey = "key-dup",
                TenantId = _tenantId,
                RequestHash = "different-hash",
                ResponseJson = "{}",
                StatusCode = 200
            });

        var request = new DocumentReviewRequestDto { Action = "submit" };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ReviewDocumentAsync(_tenantId, _projectId, _documentId, _userId, "corr-1", "key-dup", ValidEtag, request));
        Assert.Contains("Idempotency-Key", ex.Message);
    }

    [Fact]
    public async Task ReviewDocument_PersistsEventAndAudit()
    {
        SetupValidScenario();
        var request = new DocumentReviewRequestDto { Action = "startReview" };

        await _service.ReviewDocumentAsync(
            _tenantId, _projectId, _documentId, _userId, "corr-1", "key-persist", ValidEtag, request);

        _docArtifactRepoMock.Verify(r => r.UpdateReviewStatusAsync(
            It.IsAny<DocumentArtifactEntity>(), ValidEtag,
            It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _reviewEventRepoMock.Verify(r => r.CreateAsync(
            It.Is<DocumentReviewEventEntity>(e => e.Action == "startReview"),
            It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _auditRepoMock.Verify(r => r.InsertAuditLogAsync(
            It.IsAny<AuditLogEntity>(),
            It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReviewDocument_SuccessAudit_ContainsPrePostStatusReasonAndCorrelation()
    {
        SetupValidScenario();
        var request = new DocumentReviewRequestDto { Action = "startReview", Comments = "Proceed to active review." };

        await _service.ReviewDocumentAsync(
            _tenantId, _projectId, _documentId, _userId, "corr-audit-success", "key-audit-success", ValidEtag, request);

        _auditRepoMock.Verify(r => r.InsertAuditLogAsync(
            It.Is<AuditLogEntity>(a =>
                a.Outcome == "success" &&
                a.CorrelationId == "corr-audit-success" &&
                a.Reason == "Proceed to active review." &&
                a.BeforeJson != null &&
                a.BeforeJson.Contains("\"draft\"", StringComparison.OrdinalIgnoreCase) &&
                a.AfterJson != null &&
                a.AfterJson.Contains("\"inReview\"", StringComparison.OrdinalIgnoreCase)),
            It.IsAny<IDbConnection>(),
            It.IsAny<IDbTransaction>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ReviewDocument_UpdatedEtag_ReturnedInResponse()
    {
        SetupValidScenario();
        var request = new DocumentReviewRequestDto { Action = "startReview" };

        var result = await _service.ReviewDocumentAsync(
            _tenantId, _projectId, _documentId, _userId, "corr-1", "key-etag", ValidEtag, request);

        Assert.NotEqual(ValidEtag, result.Etag);
        Assert.Equal(2, int.Parse(result.Etag.Trim('"').Split('-')[0]));
    }

    [Fact]
    public async Task ReviewDocument_Failure_AuditIncludesBeforeAndAfterStatusWithoutMutation()
    {
        var draft = CreateDraftDocument();
        _projectRepoMock.Setup(r => r.ExistsAsync(_projectId, _tenantId, It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _docArtifactRepoMock.Setup(r => r.GetByIdAsync(_documentId, _projectId, _tenantId, It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(draft);
        _docArtifactRepoMock.Setup(r => r.UpdateReviewStatusAsync(
                It.IsAny<DocumentArtifactEntity>(), It.IsAny<string>(), It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("ETAG_MISMATCH:The document has been modified."));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ReviewDocumentAsync(
                _tenantId, _projectId, _documentId, _userId, "corr-fail", "key-fail", ValidEtag,
                new DocumentReviewRequestDto { Action = "submit", Comments = "comment" }));

        _auditRepoMock.Verify(r => r.InsertAuditLogAsync(
            It.Is<AuditLogEntity>(a =>
                a.Outcome == "failure" &&
                a.CorrelationId == "corr-fail" &&
                a.BeforeJson != null &&
                a.AfterJson != null &&
                a.Reason != null &&
                a.Reason.Contains("comment", StringComparison.OrdinalIgnoreCase)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReviewDocument_FailureAudit_UsesActionAsReasonWhenCommentsMissing()
    {
        SetupValidScenario();
        _docArtifactRepoMock.Setup(r => r.UpdateReviewStatusAsync(
                It.IsAny<DocumentArtifactEntity>(), It.IsAny<string>(), It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("ETAG_MISMATCH:The document has been modified."));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ReviewDocumentAsync(
                _tenantId, _projectId, _documentId, _userId, "corr-fail-no-comment", "key-fail-no-comment", ValidEtag,
                new DocumentReviewRequestDto { Action = "submit" }));

        _auditRepoMock.Verify(r => r.InsertAuditLogAsync(
            It.Is<AuditLogEntity>(a =>
                a.Outcome == "failure" &&
                a.CorrelationId == "corr-fail-no-comment" &&
                a.Reason != null &&
                a.Reason.Contains("ETAG_MISMATCH", StringComparison.OrdinalIgnoreCase)),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
