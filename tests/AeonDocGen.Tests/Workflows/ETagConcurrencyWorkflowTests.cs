// TR: LLD-0021 | ORIGIN: MID_LEVEL_DESIGN-0021
using System.Data;
using AeonDocGen.Core.DTOs;
using AeonDocGen.Core.Interfaces;
using AeonDocGen.Core.Models;
using AeonDocGen.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace AeonDocGen.Tests.Workflows;

/// <summary>
/// ETag concurrency tests for document review matching and stale If-Match cases.
/// </summary>
public class ETagConcurrencyWorkflowTests
{
    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-00000000000A");
    private readonly Guid _userId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private readonly Guid _projectId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private readonly Guid _documentId = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private readonly string _correlationId = "corr-etag-001";

    private (DocumentReviewService service,
        Mock<IDocumentArtifactRepository> docRepo) CreateService()
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

        return (service, docRepo);
    }

    private DocumentArtifactEntity CreateDocument(string etag, string reviewStatus = "draft")
    {
        return new DocumentArtifactEntity
        {
            DocumentArtifactId = _documentId,
            TenantId = _tenantId,
            ProjectId = _projectId,
            DocumentType = "narrative",
            Format = "pdf",
            ReviewStatus = reviewStatus,
            Etag = etag,
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            TemplateId = Guid.NewGuid(),
            TemplateVersion = "1.0",
            StorageUri = "blob://test",
            ChecksumSha256 = "abc",
            FooterVersionText = "v1.0"
        };
    }

    // --- ETag matches: operation succeeds ---

    [Fact]
    public async Task MatchingETag_OperationSucceeds()
    {
        var (service, docRepo) = CreateService();
        var currentEtag = "\"1-abc\"";
        var document = CreateDocument(currentEtag);

        docRepo.Setup(r => r.GetByIdAsync(_documentId, _projectId, _tenantId, It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var request = new DocumentReviewRequestDto { Action = "startReview" };
        var result = await service.ReviewDocumentAsync(
            _tenantId, _projectId, _documentId, _userId, _correlationId,
            "idem-match-001", currentEtag, request, CancellationToken.None);

        Assert.Equal("inReview", result.ReviewStatus);
        Assert.NotEqual(currentEtag, result.Etag);
    }

    // --- Stale ETag: operation fails with ETAG_MISMATCH ---

    [Fact]
    public async Task StaleETag_ThrowsEtagMismatch()
    {
        var (service, docRepo) = CreateService();
        var currentEtag = "\"2-xyz\"";
        var staleEtag = "\"1-abc\"";
        var document = CreateDocument(currentEtag);

        docRepo.Setup(r => r.GetByIdAsync(_documentId, _projectId, _tenantId, It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var request = new DocumentReviewRequestDto { Action = "startReview" };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReviewDocumentAsync(
                _tenantId, _projectId, _documentId, _userId, _correlationId,
                "idem-stale-001", staleEtag, request, CancellationToken.None));

        Assert.StartsWith("ETAG_MISMATCH:", ex.Message);
    }

    // --- ETag format preserved in response ---

    [Fact]
    public async Task ETag_IsQuotedInResponse()
    {
        var (service, docRepo) = CreateService();
        var currentEtag = "\"1-abc\"";
        var document = CreateDocument(currentEtag);

        docRepo.Setup(r => r.GetByIdAsync(_documentId, _projectId, _tenantId, It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var request = new DocumentReviewRequestDto { Action = "startReview" };
        var result = await service.ReviewDocumentAsync(
            _tenantId, _projectId, _documentId, _userId, _correlationId,
            "idem-format-001", currentEtag, request, CancellationToken.None);

        Assert.StartsWith("\"", result.Etag);
        Assert.EndsWith("\"", result.Etag);
    }

    // --- ETag version increments ---

    [Fact]
    public async Task SuccessfulReview_IncrementsDocumentVersion()
    {
        var (service, docRepo) = CreateService();
        var currentEtag = "\"1-abc\"";
        var document = CreateDocument(currentEtag);
        document.Version = 1;

        docRepo.Setup(r => r.GetByIdAsync(_documentId, _projectId, _tenantId, It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var request = new DocumentReviewRequestDto { Action = "startReview" };
        var result = await service.ReviewDocumentAsync(
            _tenantId, _projectId, _documentId, _userId, _correlationId,
            "idem-version-001", currentEtag, request, CancellationToken.None);

        // ETag has changed, indicating version was incremented
        Assert.NotEqual(currentEtag, result.Etag);
    }

    // --- Concurrent update at database level returns 0 rows ---

    [Fact]
    public async Task ConcurrentUpdateAtDbLevel_ThrowsEtagMismatch()
    {
        var (service, docRepo) = CreateService();
        var currentEtag = "\"1-abc\"";
        var document = CreateDocument(currentEtag);

        docRepo.Setup(r => r.GetByIdAsync(_documentId, _projectId, _tenantId, It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        // Simulate concurrent update: UpdateReviewStatusAsync returns 0 rows
        docRepo.Setup(r => r.UpdateReviewStatusAsync(
                It.IsAny<DocumentArtifactEntity>(), It.IsAny<string>(),
                It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var request = new DocumentReviewRequestDto { Action = "startReview" };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReviewDocumentAsync(
                _tenantId, _projectId, _documentId, _userId, _correlationId,
                "idem-concurrent-001", currentEtag, request, CancellationToken.None));

        Assert.StartsWith("ETAG_MISMATCH:", ex.Message);
    }
}
