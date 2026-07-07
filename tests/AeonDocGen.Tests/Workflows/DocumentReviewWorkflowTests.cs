// TR: LLD-0021 | ORIGIN: MID_LEVEL_DESIGN-0021
using System.Data;
using System.Text.Json;
using AeonDocGen.Core.DTOs;
using AeonDocGen.Core.Interfaces;
using AeonDocGen.Core.Models;
using AeonDocGen.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace AeonDocGen.Tests.Workflows;

/// <summary>
/// Workflow tests for document review actions: submit, startReview, approve, and reject.
/// Validates state transitions, review event creation, and audit logging.
/// </summary>
public class DocumentReviewWorkflowTests
{
    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-00000000000A");
    private readonly Guid _userId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private readonly Guid _projectId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private readonly Guid _documentId = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private readonly string _correlationId = "corr-workflow-001";
    private readonly string _etag = "\"1-abc\"";

    private (DocumentReviewService service,
        Mock<IProjectRepository> projectRepo,
        Mock<IDocumentArtifactRepository> docRepo,
        Mock<IDocumentReviewEventRepository> eventRepo,
        Mock<IAuditLogRepository> auditRepo,
        Mock<IIdempotencyRepository> idempotencyRepo) CreateService()
    {
        var projectRepo = new Mock<IProjectRepository>();
        var docRepo = new Mock<IDocumentArtifactRepository>();
        var eventRepo = new Mock<IDocumentReviewEventRepository>();
        var auditRepo = new Mock<IAuditLogRepository>();
        var idempotencyRepo = new Mock<IIdempotencyRepository>();
        var connFactory = new Mock<IDbConnectionFactory>();
        var logger = new Mock<ILogger<DocumentReviewService>>();

        var mockConnection = new Mock<IDbConnection>();
        var mockTransaction = new Mock<IDbTransaction>();
        mockConnection.Setup(c => c.Open());
        mockConnection.Setup(c => c.BeginTransaction()).Returns(mockTransaction.Object);
        connFactory.Setup(f => f.CreateConnection()).Returns(mockConnection.Object);

        projectRepo.Setup(r => r.ExistsAsync(_projectId, _tenantId, It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        docRepo.Setup(r => r.UpdateReviewStatusAsync(
                It.IsAny<DocumentArtifactEntity>(), It.IsAny<string>(),
                It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var service = new DocumentReviewService(
            projectRepo.Object, docRepo.Object, eventRepo.Object, auditRepo.Object,
            idempotencyRepo.Object, connFactory.Object, logger.Object);

        return (service, projectRepo, docRepo, eventRepo, auditRepo, idempotencyRepo);
    }

    private DocumentArtifactEntity CreateDocument(string reviewStatus)
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
            BrandingApplied = true,
            WatermarkApplied = false,
            FooterVersionText = "v1.0",
            StorageUri = "blob://test",
            ChecksumSha256 = "abc123",
            ReviewStatus = reviewStatus,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Version = 1,
            Etag = _etag
        };
    }

    // --- Submit action ---

    [Fact]
    public async Task Submit_DraftDocument_KeepsStatusDraft_RecordsEvent()
    {
        var (service, _, docRepo, eventRepo, _, _) = CreateService();
        var document = CreateDocument("draft");

        docRepo.Setup(r => r.GetByIdAsync(_documentId, _projectId, _tenantId, It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var request = new DocumentReviewRequestDto { Action = "submit", Comments = "Submitting for review." };
        var result = await service.ReviewDocumentAsync(
            _tenantId, _projectId, _documentId, _userId, _correlationId,
            "idem-submit-001", _etag, request, CancellationToken.None);

        Assert.Equal("draft", result.ReviewStatus);
        Assert.Equal("submit", result.Event.Action);
        Assert.Equal("Submitting for review.", result.Event.Comments);
        Assert.NotNull(result.Event.ReviewEventId);

        eventRepo.Verify(r => r.CreateAsync(
            It.Is<DocumentReviewEventEntity>(e => e.Action == "submit"),
            It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Submit_NonDraftDocument_Throws()
    {
        var (service, _, docRepo, _, _, _) = CreateService();
        var document = CreateDocument("inReview");

        docRepo.Setup(r => r.GetByIdAsync(_documentId, _projectId, _tenantId, It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var request = new DocumentReviewRequestDto { Action = "submit" };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReviewDocumentAsync(
                _tenantId, _projectId, _documentId, _userId, _correlationId,
                "idem-submit-002", _etag, request, CancellationToken.None));

        Assert.StartsWith("INVALID_REVIEW_ACTION:", ex.Message);
    }

    // --- StartReview action ---

    [Fact]
    public async Task StartReview_DraftDocument_TransitionsToInReview()
    {
        var (service, _, docRepo, eventRepo, _, _) = CreateService();
        var document = CreateDocument("draft");

        docRepo.Setup(r => r.GetByIdAsync(_documentId, _projectId, _tenantId, It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var request = new DocumentReviewRequestDto { Action = "startReview", Comments = "Starting review." };
        var result = await service.ReviewDocumentAsync(
            _tenantId, _projectId, _documentId, _userId, _correlationId,
            "idem-start-001", _etag, request, CancellationToken.None);

        Assert.Equal("inReview", result.ReviewStatus);
        Assert.Equal("startReview", result.Event.Action);
        Assert.NotNull(result.Etag);
        Assert.NotEqual(_etag, result.Etag);

        eventRepo.Verify(r => r.CreateAsync(
            It.Is<DocumentReviewEventEntity>(e => e.Action == "startReview"),
            It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartReview_ApprovedDocument_Throws()
    {
        var (service, _, docRepo, _, _, _) = CreateService();
        var document = CreateDocument("approved");

        docRepo.Setup(r => r.GetByIdAsync(_documentId, _projectId, _tenantId, It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var request = new DocumentReviewRequestDto { Action = "startReview" };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReviewDocumentAsync(
                _tenantId, _projectId, _documentId, _userId, _correlationId,
                "idem-start-002", _etag, request, CancellationToken.None));

        Assert.StartsWith("INVALID_REVIEW_ACTION:", ex.Message);
    }

    // --- Approve action ---

    [Fact]
    public async Task Approve_InReviewDocument_TransitionsToApproved()
    {
        var (service, _, docRepo, eventRepo, _, _) = CreateService();
        var document = CreateDocument("inReview");

        docRepo.Setup(r => r.GetByIdAsync(_documentId, _projectId, _tenantId, It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var request = new DocumentReviewRequestDto { Action = "approve", Comments = "Looks good." };
        var result = await service.ReviewDocumentAsync(
            _tenantId, _projectId, _documentId, _userId, _correlationId,
            "idem-approve-001", _etag, request, CancellationToken.None);

        Assert.Equal("approved", result.ReviewStatus);
        Assert.Equal("approve", result.Event.Action);
        Assert.Equal(_userId.ToString(), result.ReviewedByUserId);
        Assert.NotNull(result.ReviewedAt);

        eventRepo.Verify(r => r.CreateAsync(
            It.Is<DocumentReviewEventEntity>(e => e.Action == "approve" && e.ActorUserId == _userId),
            It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Approve_DraftDocument_Throws()
    {
        var (service, _, docRepo, _, _, _) = CreateService();
        var document = CreateDocument("draft");

        docRepo.Setup(r => r.GetByIdAsync(_documentId, _projectId, _tenantId, It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var request = new DocumentReviewRequestDto { Action = "approve" };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReviewDocumentAsync(
                _tenantId, _projectId, _documentId, _userId, _correlationId,
                "idem-approve-002", _etag, request, CancellationToken.None));

        Assert.StartsWith("INVALID_REVIEW_ACTION:", ex.Message);
        Assert.Contains("draft", ex.Message);
    }

    [Fact]
    public async Task Approve_RejectedDocument_Throws()
    {
        var (service, _, docRepo, _, _, _) = CreateService();
        var document = CreateDocument("rejected");

        docRepo.Setup(r => r.GetByIdAsync(_documentId, _projectId, _tenantId, It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var request = new DocumentReviewRequestDto { Action = "approve" };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReviewDocumentAsync(
                _tenantId, _projectId, _documentId, _userId, _correlationId,
                "idem-approve-003", _etag, request, CancellationToken.None));
    }

    // --- Reject action ---

    [Fact]
    public async Task Reject_InReviewDocument_TransitionsToRejected()
    {
        var (service, _, docRepo, eventRepo, _, _) = CreateService();
        var document = CreateDocument("inReview");

        docRepo.Setup(r => r.GetByIdAsync(_documentId, _projectId, _tenantId, It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var request = new DocumentReviewRequestDto { Action = "reject", Comments = "Needs revision." };
        var result = await service.ReviewDocumentAsync(
            _tenantId, _projectId, _documentId, _userId, _correlationId,
            "idem-reject-001", _etag, request, CancellationToken.None);

        Assert.Equal("rejected", result.ReviewStatus);
        Assert.Equal("reject", result.Event.Action);
        Assert.Equal("Needs revision.", result.Event.Comments);
        Assert.Equal(_userId.ToString(), result.ReviewedByUserId);
        Assert.NotNull(result.ReviewedAt);

        eventRepo.Verify(r => r.CreateAsync(
            It.Is<DocumentReviewEventEntity>(e => e.Action == "reject" && e.Comments == "Needs revision."),
            It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Reject_DraftDocument_Throws()
    {
        var (service, _, docRepo, _, _, _) = CreateService();
        var document = CreateDocument("draft");

        docRepo.Setup(r => r.GetByIdAsync(_documentId, _projectId, _tenantId, It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var request = new DocumentReviewRequestDto { Action = "reject", Comments = "Not ready." };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReviewDocumentAsync(
                _tenantId, _projectId, _documentId, _userId, _correlationId,
                "idem-reject-002", _etag, request, CancellationToken.None));

        Assert.StartsWith("INVALID_REVIEW_ACTION:", ex.Message);
    }

    // --- Audit log verification ---

    [Fact]
    public async Task AllReviewActions_CreateAuditLogEntry()
    {
        var (service, _, docRepo, _, auditRepo, _) = CreateService();
        var document = CreateDocument("inReview");

        docRepo.Setup(r => r.GetByIdAsync(_documentId, _projectId, _tenantId, It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var request = new DocumentReviewRequestDto { Action = "approve", Comments = "Approved." };
        await service.ReviewDocumentAsync(
            _tenantId, _projectId, _documentId, _userId, _correlationId,
            "idem-audit-001", _etag, request, CancellationToken.None);

        auditRepo.Verify(r => r.InsertAuditLogAsync(
            It.Is<AuditLogEntity>(a =>
                a.TenantId == _tenantId &&
                a.ActorUserId == _userId &&
                a.Action == "documents.review.approve" &&
                a.ResourceType == "DocumentArtifact" &&
                a.Outcome == "success" &&
                a.CorrelationId == _correlationId),
            It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // --- Invalid action name ---

    [Fact]
    public async Task InvalidAction_Throws()
    {
        var (service, _, docRepo, _, _, _) = CreateService();
        var document = CreateDocument("draft");

        docRepo.Setup(r => r.GetByIdAsync(_documentId, _projectId, _tenantId, It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var request = new DocumentReviewRequestDto { Action = "invalidAction" };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReviewDocumentAsync(
                _tenantId, _projectId, _documentId, _userId, _correlationId,
                "idem-invalid-001", _etag, request, CancellationToken.None));

        Assert.StartsWith("INVALID_REVIEW_ACTION:", ex.Message);
    }

    // --- Empty action ---

    [Fact]
    public async Task EmptyAction_Throws()
    {
        var (service, _, _, _, _, _) = CreateService();

        var request = new DocumentReviewRequestDto { Action = "" };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReviewDocumentAsync(
                _tenantId, _projectId, _documentId, _userId, _correlationId,
                "idem-empty-001", _etag, request, CancellationToken.None));

        Assert.StartsWith("INVALID_REQUEST_BODY:", ex.Message);
    }

    // --- Etag update on successful transition ---

    [Fact]
    public async Task SuccessfulTransition_ReturnsUpdatedEtag()
    {
        var (service, _, docRepo, _, _, _) = CreateService();
        var document = CreateDocument("draft");

        docRepo.Setup(r => r.GetByIdAsync(_documentId, _projectId, _tenantId, It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var request = new DocumentReviewRequestDto { Action = "startReview" };
        var result = await service.ReviewDocumentAsync(
            _tenantId, _projectId, _documentId, _userId, _correlationId,
            "idem-etag-001", _etag, request, CancellationToken.None);

        Assert.NotEqual(_etag, result.Etag);
        Assert.StartsWith("\"", result.Etag);
    }

    // --- Comments length validation ---

    [Fact]
    public async Task CommentsTooLong_Throws()
    {
        var (service, _, _, _, _, _) = CreateService();

        var longComment = new string('x', 2001);
        var request = new DocumentReviewRequestDto { Action = "submit", Comments = longComment };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReviewDocumentAsync(
                _tenantId, _projectId, _documentId, _userId, _correlationId,
                "idem-comment-001", _etag, request, CancellationToken.None));

        Assert.Contains("2000", ex.Message);
    }

    // --- Project not found ---

    [Fact]
    public async Task ProjectNotFound_Throws()
    {
        var (service, projectRepo, _, _, _, _) = CreateService();

        projectRepo.Setup(r => r.ExistsAsync(_projectId, _tenantId, It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var request = new DocumentReviewRequestDto { Action = "submit" };

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.ReviewDocumentAsync(
                _tenantId, _projectId, _documentId, _userId, _correlationId,
                "idem-proj-001", _etag, request, CancellationToken.None));

        Assert.StartsWith("PROJECT_NOT_FOUND:", ex.Message);
    }

    // --- Document not found ---

    [Fact]
    public async Task DocumentNotFound_Throws()
    {
        var (service, _, docRepo, _, _, _) = CreateService();

        docRepo.Setup(r => r.GetByIdAsync(_documentId, _projectId, _tenantId, It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DocumentArtifactEntity?)null);

        var request = new DocumentReviewRequestDto { Action = "submit" };

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.ReviewDocumentAsync(
                _tenantId, _projectId, _documentId, _userId, _correlationId,
                "idem-doc-001", _etag, request, CancellationToken.None));

        Assert.StartsWith("DOCUMENT_NOT_FOUND:", ex.Message);
    }
}
