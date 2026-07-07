// TR: LLD-0029 | ORIGIN: MID_LEVEL_DESIGN-0029
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AeonDocGen.Core.DTOs;
using AeonDocGen.Core.Interfaces;
using AeonDocGen.Core.Models;
using AeonDocGen.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace AeonDocGen.Tests.Workflows;

/// <summary>
/// Idempotency tests for branding upload, document generation retry behavior,
/// and document review duplicate retry and payload mismatch behavior.
/// </summary>
public class IdempotencyWorkflowTests
{
    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-00000000000A");
    private readonly Guid _userId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private readonly Guid _projectId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private readonly Guid _documentId = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private readonly string _correlationId = "corr-idem-wf-001";

    // --- Branding upload idempotency ---

    [Fact]
    public async Task BrandingUpload_IdempotentReplay_ReturnsPreviousResponse()
    {
        var brandingRepo = new Mock<IBrandingAssetRepository>();
        var storageClient = new Mock<IBrandingStorageClient>();
        var malwareClient = new Mock<IMalwareScannerClient>();
        var auditRepo = new Mock<IAuditLogRepository>();
        var idempotencyRepo = new Mock<IIdempotencyRepository>();
        var connFactory = new Mock<IDbConnectionFactory>();
        var logger = new Mock<ILogger<BrandingAssetService>>();

        var settings = Options.Create(new BrandingUploadSettings
        {
            AllowedLogoMimeTypes = new[] { "image/png" },
            AllowedFontExtensions = new[] { ".ttf" },
            MaxLogoSizeBytes = 5 * 1024 * 1024,
            MaxFontsZipSizeBytes = 20 * 1024 * 1024
        });

        var previousResponse = new BrandingAssetResponseDto
        {
            Id = "brand-prev",
            TenantId = _tenantId.ToString(),
            Status = "updated",
            Version = 1,
            Etag = "\"1-brand\""
        };

        var input = new BrandingUploadInput
        {
            ColorsJson = "{\"primary\":\"#000\"}"
        };

        // Compute expected hash matching BrandingAssetService.ComputeRequestHash
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        if (input.LogoData != null) sha.AppendData(input.LogoData);
        sha.AppendData(Encoding.UTF8.GetBytes(input.ColorsJson ?? string.Empty));
        if (input.FontsZipData != null) sha.AppendData(input.FontsZipData);
        var requestHash = Convert.ToHexStringLower(sha.GetHashAndReset());

        idempotencyRepo.Setup(r => r.GetByKeyAsync("idem-brand-replay", _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdempotencyRecordEntity
            {
                IdempotencyKey = "idem-brand-replay",
                TenantId = _tenantId,
                RequestHash = requestHash,
                ResponseJson = JsonSerializer.Serialize(previousResponse),
                StatusCode = 201,
                CreatedAt = DateTime.UtcNow
            });

        var service = new BrandingAssetService(
            brandingRepo.Object, auditRepo.Object, idempotencyRepo.Object, storageClient.Object,
            malwareClient.Object, connFactory.Object, settings, logger.Object);

        var result = await service.UploadBrandingAssetsAsync(
            _tenantId, _userId, _correlationId, "idem-brand-replay", input, CancellationToken.None);

        Assert.Equal("brand-prev", result.Id);
        brandingRepo.Verify(r => r.CreateAsync(It.IsAny<BrandingAssetEntity>(), It.IsAny<IDbConnection>(),
            It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- Document review: duplicate retry with matching payload ---

    [Fact]
    public async Task DocumentReview_DuplicateRetry_MatchingPayload_ReturnsPreviousResponse()
    {
        var projectRepo = new Mock<IProjectRepository>();
        var docRepo = new Mock<IDocumentArtifactRepository>();
        var eventRepo = new Mock<IDocumentReviewEventRepository>();
        var auditRepo = new Mock<IAuditLogRepository>();
        var idempotencyRepo = new Mock<IIdempotencyRepository>();
        var connFactory = new Mock<IDbConnectionFactory>();
        var logger = new Mock<ILogger<DocumentReviewService>>();

        projectRepo.Setup(r => r.ExistsAsync(_projectId, _tenantId, It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var previousResponse = new DocumentReviewResponseDto
        {
            DocumentId = _documentId.ToString(),
            ProjectId = _projectId.ToString(),
            ReviewStatus = "inReview",
            Etag = "\"2-def\""
        };

        var request = new DocumentReviewRequestDto { Action = "startReview", Comments = null };

        // Compute expected hash
        var payload = $"{_projectId}|{_documentId}|{request.Action}|{request.Comments ?? ""}";
        var hashBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payload));
        var requestHash = Convert.ToHexStringLower(hashBytes);

        idempotencyRepo.Setup(r => r.GetByKeyAsync("idem-review-dup", _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdempotencyRecordEntity
            {
                IdempotencyKey = "idem-review-dup",
                TenantId = _tenantId,
                RequestHash = requestHash,
                ResponseJson = JsonSerializer.Serialize(previousResponse),
                StatusCode = 200,
                CreatedAt = DateTime.UtcNow
            });

        var service = new DocumentReviewService(
            projectRepo.Object, docRepo.Object, eventRepo.Object, auditRepo.Object,
            idempotencyRepo.Object, connFactory.Object, logger.Object);

        var result = await service.ReviewDocumentAsync(
            _tenantId, _projectId, _documentId, _userId, _correlationId,
            "idem-review-dup", "\"1-abc\"", request, CancellationToken.None);

        Assert.Equal("inReview", result.ReviewStatus);
        docRepo.Verify(r => r.UpdateReviewStatusAsync(It.IsAny<DocumentArtifactEntity>(), It.IsAny<string>(),
            It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- Document review: idempotency key reused with different payload ---

    [Fact]
    public async Task DocumentReview_IdempotencyKeyReused_DifferentPayload_Throws()
    {
        var projectRepo = new Mock<IProjectRepository>();
        var docRepo = new Mock<IDocumentArtifactRepository>();
        var eventRepo = new Mock<IDocumentReviewEventRepository>();
        var auditRepo = new Mock<IAuditLogRepository>();
        var idempotencyRepo = new Mock<IIdempotencyRepository>();
        var connFactory = new Mock<IDbConnectionFactory>();
        var logger = new Mock<ILogger<DocumentReviewService>>();

        idempotencyRepo.Setup(r => r.GetByKeyAsync("idem-review-mismatch", _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdempotencyRecordEntity
            {
                IdempotencyKey = "idem-review-mismatch",
                TenantId = _tenantId,
                RequestHash = "different-hash-value",
                ResponseJson = "{}",
                StatusCode = 200,
                CreatedAt = DateTime.UtcNow
            });

        var service = new DocumentReviewService(
            projectRepo.Object, docRepo.Object, eventRepo.Object, auditRepo.Object,
            idempotencyRepo.Object, connFactory.Object, logger.Object);

        var request = new DocumentReviewRequestDto { Action = "approve" };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReviewDocumentAsync(
                _tenantId, _projectId, _documentId, _userId, _correlationId,
                "idem-review-mismatch", "\"1-abc\"", request, CancellationToken.None));

        Assert.Contains("Idempotency-Key", ex.Message);
    }

    // --- Document generation: idempotency key mismatch ---

    [Fact]
    public async Task DocumentGeneration_IdempotencyKeyReused_DifferentPayload_Throws()
    {
        var projectRepo = new Mock<IProjectRepository>();
        var templateRepo = new Mock<ITemplateResolutionRepository>();
        var sourceRepo = new Mock<ISourceEntityRepository>();
        var brandingRepo = new Mock<IBrandingAssetRepository>();
        var docRepo = new Mock<IDocumentArtifactRepository>();
        var docSourceRepo = new Mock<IDocumentSourceRepository>();
        var auditRepo = new Mock<IAuditLogRepository>();
        var idempotencyRepo = new Mock<IIdempotencyRepository>();
        var storageClient = new Mock<IDocumentStorageClient>();
        var connFactory = new Mock<IDbConnectionFactory>();
        var logger = new Mock<ILogger<DocumentGenerationService>>();

        var settings = Options.Create(new DocumentGenerationSettings
        {
            FooterVersionFormat = "v1.0",
            MaxWatermarkLength = 100,
            EnableInlineRenderWorkerFallback = true
        });

        idempotencyRepo.Setup(r => r.GetByKeyAsync("idem-gen-mismatch", _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdempotencyRecordEntity
            {
                IdempotencyKey = "idem-gen-mismatch",
                TenantId = _tenantId,
                RequestHash = "different-hash-value",
                ResponseJson = "{}",
                StatusCode = 201,
                CreatedAt = DateTime.UtcNow
            });

        var service = new DocumentGenerationService(
            projectRepo.Object, templateRepo.Object, sourceRepo.Object, brandingRepo.Object,
            docRepo.Object, docSourceRepo.Object, auditRepo.Object, idempotencyRepo.Object,
            storageClient.Object, connFactory.Object, settings, logger.Object);

        var request = new GenerateDocumentRequestDto
        {
            DocumentType = "narrative",
            Format = "pdf",
            TemplateId = Guid.NewGuid().ToString(),
            IncludeBranding = false,
            SourceIds = new List<string> { Guid.NewGuid().ToString() }
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GenerateDocumentAsync(
                _tenantId, _projectId, _userId, _correlationId, "idem-gen-mismatch", request, CancellationToken.None));

        Assert.Contains("Idempotency-Key", ex.Message);
    }
}
