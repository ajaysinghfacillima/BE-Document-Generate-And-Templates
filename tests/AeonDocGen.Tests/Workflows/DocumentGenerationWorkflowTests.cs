// TR: LLD-0020 | ORIGIN: MID_LEVEL_DESIGN-0020
using System.Data;
using AeonDocGen.Core.DTOs;
using AeonDocGen.Core.Interfaces;
using AeonDocGen.Core.Models;
using AeonDocGen.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace AeonDocGen.Tests.Workflows;

/// <summary>
/// Document generation tests for branding/no-branding and watermark/no-watermark outcomes,
/// template/version resolution, source validation, and duplicate artifact prevention during retries.
/// </summary>
public class DocumentGenerationWorkflowTests
{
    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-00000000000A");
    private readonly Guid _userId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private readonly Guid _projectId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private readonly Guid _templateId = Guid.Parse("00000000-0000-0000-0000-000000000004");
    private readonly string _correlationId = "corr-gen-wf-001";

    private (DocumentGenerationService service,
        Mock<IProjectRepository> projectRepo,
        Mock<ITemplateResolutionRepository> templateRepo,
        Mock<ISourceEntityRepository> sourceRepo,
        Mock<IBrandingAssetRepository> brandingRepo,
        Mock<IDocumentArtifactRepository> docRepo,
        Mock<IDocumentSourceRepository> docSourceRepo,
        Mock<IAuditLogRepository> auditRepo,
        Mock<IIdempotencyRepository> idempotencyRepo,
        Mock<IDocumentStorageClient> storageClient) CreateService()
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

        var mockConnection = new Mock<IDbConnection>();
        var mockTransaction = new Mock<IDbTransaction>();
        mockConnection.Setup(c => c.Open());
        mockConnection.Setup(c => c.BeginTransaction()).Returns(mockTransaction.Object);
        connFactory.Setup(f => f.CreateConnection()).Returns(mockConnection.Object);

        var settings = Options.Create(new DocumentGenerationSettings
        {
            FooterVersionFormat = "v1.0",
            MaxWatermarkLength = 100,
            EnableInlineRenderWorkerFallback = true
        });

        projectRepo.Setup(r => r.ExistsAsync(_projectId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        storageClient.Setup(s => s.StoreFileAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("blob://generated/doc.pdf");

        var service = new DocumentGenerationService(
            projectRepo.Object, templateRepo.Object, sourceRepo.Object, brandingRepo.Object,
            docRepo.Object, docSourceRepo.Object, auditRepo.Object, idempotencyRepo.Object,
            storageClient.Object, connFactory.Object, settings, logger.Object);

        return (service, projectRepo, templateRepo, sourceRepo, brandingRepo, docRepo, docSourceRepo, auditRepo, idempotencyRepo, storageClient);
    }

    private void SetupTemplateResolution(Mock<ITemplateResolutionRepository> templateRepo)
    {
        templateRepo.Setup(r => r.GetByIdAsync(_templateId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TemplateEntity
            {
                TemplateId = _templateId,
                TenantId = _tenantId,
                Name = "Test Template",
                DocumentType = "narrative",
                CurrentVersion = "1.0",
                IsActive = true
            });

        templateRepo.Setup(r => r.GetLatestPublishedVersionAsync(_templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TemplateVersionEntity
            {
                TemplateVersionId = Guid.NewGuid(),
                TemplateId = _templateId,
                TemplateVersion = "1.0",
                CreatedAt = DateTime.UtcNow
            });
    }

    private void SetupSourceResolution(Mock<ISourceEntityRepository> sourceRepo, params string[] sourceIds)
    {
        var map = sourceIds
            .Select(id => Guid.TryParse(id, out var parsed) ? parsed : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToDictionary(id => id, _ => "artifact");

        sourceRepo.Setup(r => r.ResolveSourceEntityTypesAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), _projectId, _tenantId, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids, Guid _, Guid _, Guid _, CancellationToken _) =>
                ids.Where(map.ContainsKey).ToDictionary(id => id, id => map[id]));
    }

    // --- Branding applied when available ---

    [Fact]
    public async Task BrandingRequested_AndAvailable_BrandingAppliedTrue()
    {
        var (service, _, templateRepo, sourceRepo, brandingRepo, _, _, _, _, _) = CreateService();
        SetupTemplateResolution(templateRepo);

        var sourceId = Guid.NewGuid();
        SetupSourceResolution(sourceRepo, sourceId.ToString());

        brandingRepo.Setup(r => r.GetByTenantIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BrandingAssetEntity
            {
                BrandingAssetId = Guid.NewGuid(),
                TenantId = _tenantId,
                Status = "active",
                LogoStorageUri = "blob://logo",
                ColorsJson = "{\"primary\":\"#000\"}"
            });

        var request = new GenerateDocumentRequestDto
        {
            DocumentType = "narrative",
            Format = "pdf",
            TemplateId = _templateId.ToString(),
            IncludeBranding = true,
            SourceIds = new List<string> { sourceId.ToString() }
        };

        var result = await service.GenerateDocumentAsync(
            _tenantId, _projectId, _userId, _correlationId, "idem-brand-001", request, CancellationToken.None);

        Assert.True(result.BrandingApplied);
    }

    // --- Branding requested but not available ---

    [Fact]
    public async Task BrandingRequested_NotAvailable_BrandingAppliedFalse()
    {
        var (service, _, templateRepo, sourceRepo, brandingRepo, _, _, _, _, _) = CreateService();
        SetupTemplateResolution(templateRepo);

        var sourceId = Guid.NewGuid();
        SetupSourceResolution(sourceRepo, sourceId.ToString());

        brandingRepo.Setup(r => r.GetByTenantIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BrandingAssetEntity?)null);

        var request = new GenerateDocumentRequestDto
        {
            DocumentType = "narrative",
            Format = "pdf",
            TemplateId = _templateId.ToString(),
            IncludeBranding = true,
            SourceIds = new List<string> { sourceId.ToString() }
        };

        var result = await service.GenerateDocumentAsync(
            _tenantId, _projectId, _userId, _correlationId, "idem-nobrand-001", request, CancellationToken.None);

        Assert.False(result.BrandingApplied);
    }

    // --- No branding requested ---

    [Fact]
    public async Task NoBrandingRequested_BrandingAppliedFalse()
    {
        var (service, _, templateRepo, sourceRepo, _, _, _, _, _, _) = CreateService();
        SetupTemplateResolution(templateRepo);

        var sourceId = Guid.NewGuid();
        SetupSourceResolution(sourceRepo, sourceId.ToString());

        var request = new GenerateDocumentRequestDto
        {
            DocumentType = "narrative",
            Format = "pdf",
            TemplateId = _templateId.ToString(),
            IncludeBranding = false,
            SourceIds = new List<string> { sourceId.ToString() }
        };

        var result = await service.GenerateDocumentAsync(
            _tenantId, _projectId, _userId, _correlationId, "idem-nobrand-002", request, CancellationToken.None);

        Assert.False(result.BrandingApplied);
    }

    // --- Watermark applied ---

    [Fact]
    public async Task WatermarkProvided_WatermarkAppliedTrue()
    {
        var (service, _, templateRepo, sourceRepo, _, _, _, _, _, _) = CreateService();
        SetupTemplateResolution(templateRepo);

        var sourceId = Guid.NewGuid();
        SetupSourceResolution(sourceRepo, sourceId.ToString());

        var request = new GenerateDocumentRequestDto
        {
            DocumentType = "narrative",
            Format = "pdf",
            TemplateId = _templateId.ToString(),
            IncludeBranding = false,
            WatermarkText = "Draft",
            SourceIds = new List<string> { sourceId.ToString() }
        };

        var result = await service.GenerateDocumentAsync(
            _tenantId, _projectId, _userId, _correlationId, "idem-wm-001", request, CancellationToken.None);

        Assert.True(result.WatermarkApplied);
    }

    // --- No watermark ---

    [Fact]
    public async Task NoWatermark_WatermarkAppliedFalse()
    {
        var (service, _, templateRepo, sourceRepo, _, _, _, _, _, _) = CreateService();
        SetupTemplateResolution(templateRepo);

        var sourceId = Guid.NewGuid();
        SetupSourceResolution(sourceRepo, sourceId.ToString());

        var request = new GenerateDocumentRequestDto
        {
            DocumentType = "narrative",
            Format = "pdf",
            TemplateId = _templateId.ToString(),
            IncludeBranding = false,
            SourceIds = new List<string> { sourceId.ToString() }
        };

        var result = await service.GenerateDocumentAsync(
            _tenantId, _projectId, _userId, _correlationId, "idem-nowm-001", request, CancellationToken.None);

        Assert.False(result.WatermarkApplied);
    }

    // --- Template version resolution ---

    [Fact]
    public async Task TemplateVersionResolved_PinnedInResponse()
    {
        var (service, _, templateRepo, sourceRepo, _, _, _, _, _, _) = CreateService();
        SetupTemplateResolution(templateRepo);

        var sourceId = Guid.NewGuid();
        SetupSourceResolution(sourceRepo, sourceId.ToString());

        var request = new GenerateDocumentRequestDto
        {
            DocumentType = "narrative",
            Format = "pdf",
            TemplateId = _templateId.ToString(),
            IncludeBranding = false,
            SourceIds = new List<string> { sourceId.ToString() }
        };

        var result = await service.GenerateDocumentAsync(
            _tenantId, _projectId, _userId, _correlationId, "idem-ver-001", request, CancellationToken.None);

        Assert.Equal("1.0", result.TemplateVersion);
    }

    // --- Template not found ---

    [Fact]
    public async Task TemplateNotFound_Throws()
    {
        var (service, _, templateRepo, _, _, _, _, _, _, _) = CreateService();

        templateRepo.Setup(r => r.GetByIdAsync(_templateId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TemplateEntity?)null);

        var request = new GenerateDocumentRequestDto
        {
            DocumentType = "narrative",
            Format = "pdf",
            TemplateId = _templateId.ToString(),
            IncludeBranding = false,
            SourceIds = new List<string> { Guid.NewGuid().ToString() }
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GenerateDocumentAsync(
                _tenantId, _projectId, _userId, _correlationId, "idem-tmpl-001", request, CancellationToken.None));
    }

    // --- Inactive template ---

    [Fact]
    public async Task InactiveTemplate_Throws()
    {
        var (service, _, templateRepo, _, _, _, _, _, _, _) = CreateService();

        templateRepo.Setup(r => r.GetByIdAsync(_templateId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TemplateEntity
            {
                TemplateId = _templateId,
                TenantId = _tenantId,
                Name = "Inactive",
                DocumentType = "narrative",
                IsActive = false
            });

        var request = new GenerateDocumentRequestDto
        {
            DocumentType = "narrative",
            Format = "pdf",
            TemplateId = _templateId.ToString(),
            IncludeBranding = false,
            SourceIds = new List<string> { Guid.NewGuid().ToString() }
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GenerateDocumentAsync(
                _tenantId, _projectId, _userId, _correlationId, "idem-inactive-001", request, CancellationToken.None));
    }

    // --- Source validation: invalid source ---

    [Fact]
    public async Task InvalidSourceId_Throws()
    {
        var (service, _, templateRepo, sourceRepo, _, _, _, _, _, _) = CreateService();
        SetupTemplateResolution(templateRepo);

        var sourceId = Guid.NewGuid();
        sourceRepo.Setup(r => r.ResolveSourceEntityTypesAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), _projectId, _tenantId, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>());

        var request = new GenerateDocumentRequestDto
        {
            DocumentType = "narrative",
            Format = "pdf",
            TemplateId = _templateId.ToString(),
            IncludeBranding = false,
            SourceIds = new List<string> { sourceId.ToString() }
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GenerateDocumentAsync(
                _tenantId, _projectId, _userId, _correlationId, "idem-src-001", request, CancellationToken.None));

        Assert.Contains(sourceId.ToString(), ex.Message);
    }

    // --- Review status starts as draft ---

    [Fact]
    public async Task GeneratedDocument_ReviewStatus_IsDraft()
    {
        var (service, _, templateRepo, sourceRepo, _, _, _, _, _, _) = CreateService();
        SetupTemplateResolution(templateRepo);

        var sourceId = Guid.NewGuid();
        SetupSourceResolution(sourceRepo, sourceId.ToString());

        var request = new GenerateDocumentRequestDto
        {
            DocumentType = "narrative",
            Format = "pdf",
            TemplateId = _templateId.ToString(),
            IncludeBranding = false,
            SourceIds = new List<string> { sourceId.ToString() }
        };

        var result = await service.GenerateDocumentAsync(
            _tenantId, _projectId, _userId, _correlationId, "idem-draft-001", request, CancellationToken.None);

        Assert.Equal("draft", result.ReviewStatus);
    }

    // --- Checksum is populated ---

    [Fact]
    public async Task GeneratedDocument_HasChecksumSha256()
    {
        var (service, _, templateRepo, sourceRepo, _, _, _, _, _, _) = CreateService();
        SetupTemplateResolution(templateRepo);

        var sourceId = Guid.NewGuid();
        SetupSourceResolution(sourceRepo, sourceId.ToString());

        var request = new GenerateDocumentRequestDto
        {
            DocumentType = "narrative",
            Format = "pdf",
            TemplateId = _templateId.ToString(),
            IncludeBranding = false,
            SourceIds = new List<string> { sourceId.ToString() }
        };

        var result = await service.GenerateDocumentAsync(
            _tenantId, _projectId, _userId, _correlationId, "idem-checksum-001", request, CancellationToken.None);

        Assert.NotNull(result.ChecksumSha256);
        Assert.NotEmpty(result.ChecksumSha256);
    }

    // --- Duplicate artifact prevention during idempotent retry ---

    [Fact]
    public async Task IdempotentRetry_SamePayload_ReturnsSameResponse()
    {
        var (service, _, templateRepo, sourceRepo, _, _, _, _, idempotencyRepo, _) = CreateService();
        SetupTemplateResolution(templateRepo);

        var sourceId = Guid.NewGuid();
        SetupSourceResolution(sourceRepo, sourceId.ToString());

        var existingRecord = new IdempotencyRecordEntity
        {
            IdempotencyKey = "idem-dup-001",
            TenantId = _tenantId,
            ResponseJson = System.Text.Json.JsonSerializer.Serialize(new DocumentArtifactResponseDto
            {
                Id = "doc-existing",
                DocumentType = "narrative",
                Format = "pdf",
                ReviewStatus = "draft"
            }),
            StatusCode = 201,
            CreatedAt = DateTime.UtcNow
        };

        // Compute the same hash
        var request = new GenerateDocumentRequestDto
        {
            DocumentType = "narrative",
            Format = "pdf",
            TemplateId = _templateId.ToString(),
            IncludeBranding = false,
            SourceIds = new List<string> { sourceId.ToString() }
        };

        var payload = $"{_projectId}|{request.DocumentType}|{request.Format}|{request.TemplateId}|{request.IncludeBranding}|{request.WatermarkText ?? ""}|{string.Join(",", request.SourceIds)}";
        var hashBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payload));
        existingRecord.RequestHash = Convert.ToHexStringLower(hashBytes);

        idempotencyRepo.Setup(r => r.GetByKeyAsync("idem-dup-001", _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingRecord);

        var result = await service.GenerateDocumentAsync(
            _tenantId, _projectId, _userId, _correlationId, "idem-dup-001", request, CancellationToken.None);

        Assert.Equal("doc-existing", result.Id);
    }

    // --- Project not found ---

    [Fact]
    public async Task ProjectNotFound_Throws()
    {
        var (service, projectRepo, _, _, _, _, _, _, _, _) = CreateService();

        projectRepo.Setup(r => r.ExistsAsync(_projectId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var request = new GenerateDocumentRequestDto
        {
            DocumentType = "narrative",
            Format = "pdf",
            TemplateId = _templateId.ToString(),
            IncludeBranding = false,
            SourceIds = new List<string> { Guid.NewGuid().ToString() }
        };

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.GenerateDocumentAsync(
                _tenantId, _projectId, _userId, _correlationId, "idem-proj-001", request, CancellationToken.None));
    }

    // --- Footer version text ---

    [Fact]
    public async Task GeneratedDocument_HasFooterVersionText()
    {
        var (service, _, templateRepo, sourceRepo, _, _, _, _, _, _) = CreateService();
        SetupTemplateResolution(templateRepo);

        var sourceId = Guid.NewGuid();
        SetupSourceResolution(sourceRepo, sourceId.ToString());

        var request = new GenerateDocumentRequestDto
        {
            DocumentType = "narrative",
            Format = "pdf",
            TemplateId = _templateId.ToString(),
            IncludeBranding = false,
            SourceIds = new List<string> { sourceId.ToString() }
        };

        var result = await service.GenerateDocumentAsync(
            _tenantId, _projectId, _userId, _correlationId, "idem-footer-001", request, CancellationToken.None);

        Assert.Equal("v1.0-1.0", result.FooterVersionText);
    }
}
