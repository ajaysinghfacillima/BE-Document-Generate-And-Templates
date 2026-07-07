using System.Data;
using System.Text.Json;
using AeonDocGen.Core.DTOs;
using AeonDocGen.Core.Interfaces;
using AeonDocGen.Core.Models;
using AeonDocGen.Core.Services;
using AeonDocGen.Core.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace AeonDocGen.Tests;

public class DocumentGenerationServiceTests
{
    private readonly Mock<IProjectRepository> _projectRepoMock;
    private readonly Mock<ITemplateResolutionRepository> _templateRepoMock;
    private readonly Mock<ISourceEntityRepository> _sourceRepoMock;
    private readonly Mock<IBrandingAssetRepository> _brandingRepoMock;
    private readonly Mock<IDocumentArtifactRepository> _docArtifactRepoMock;
    private readonly Mock<IDocumentSourceRepository> _docSourceRepoMock;
    private readonly Mock<IAuditLogRepository> _auditRepoMock;
    private readonly Mock<IIdempotencyRepository> _idempotencyRepoMock;
    private readonly Mock<IDocumentStorageClient> _storageClientMock;
    private readonly Mock<IDbConnectionFactory> _connFactoryMock;
    private readonly Mock<IDbConnection> _connectionMock;
    private readonly Mock<IDbTransaction> _transactionMock;
    private readonly DocumentGenerationService _service;
    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private readonly Guid _userId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private readonly Guid _projectId = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private readonly Guid _templateId = Guid.Parse("00000000-0000-0000-0000-000000000004");
    private readonly Guid _sourceId1 = Guid.Parse("00000000-0000-0000-0000-000000000005");
    private byte[]? _lastStoredContent;

    public DocumentGenerationServiceTests()
    {
        _projectRepoMock = new Mock<IProjectRepository>();
        _templateRepoMock = new Mock<ITemplateResolutionRepository>();
        _sourceRepoMock = new Mock<ISourceEntityRepository>();
        _brandingRepoMock = new Mock<IBrandingAssetRepository>();
        _docArtifactRepoMock = new Mock<IDocumentArtifactRepository>();
        _docSourceRepoMock = new Mock<IDocumentSourceRepository>();
        _auditRepoMock = new Mock<IAuditLogRepository>();
        _idempotencyRepoMock = new Mock<IIdempotencyRepository>();
        _storageClientMock = new Mock<IDocumentStorageClient>();
        _connFactoryMock = new Mock<IDbConnectionFactory>();
        _connectionMock = new Mock<IDbConnection>();
        _transactionMock = new Mock<IDbTransaction>();

        _connectionMock.Setup(c => c.BeginTransaction()).Returns(_transactionMock.Object);
        _connFactoryMock.Setup(f => f.CreateConnection()).Returns(_connectionMock.Object);

        _storageClientMock.Setup(s => s.StoreFileAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Callback<string, byte[], CancellationToken>((_, content, _) => _lastStoredContent = content)
            .ReturnsAsync((string path, byte[] _, CancellationToken _) => $"nas://{path}");

        var settings = Options.Create(new DocumentGenerationSettings
        {
            StorageBasePath = "/data/documents",
            MaxWatermarkLength = 200,
            FooterVersionFormat = "v1.0",
            IdempotencyRetentionHours = 24,
            EnableInlineRenderWorkerFallback = true
        });

        _service = new DocumentGenerationService(
            _projectRepoMock.Object,
            _templateRepoMock.Object,
            _sourceRepoMock.Object,
            _brandingRepoMock.Object,
            _docArtifactRepoMock.Object,
            _docSourceRepoMock.Object,
            _auditRepoMock.Object,
            _idempotencyRepoMock.Object,
            _storageClientMock.Object,
            _connFactoryMock.Object,
            settings,
            new Mock<ILogger<DocumentGenerationService>>().Object);
    }

    private GenerateDocumentRequestDto CreateValidRequest()
    {
        return new GenerateDocumentRequestDto
        {
            DocumentType = "narrative",
            Format = "pdf",
            TemplateId = _templateId.ToString(),
            IncludeBranding = false,
            SourceIds = new List<string> { _sourceId1.ToString() }
        };
    }

    private void SetupValidScenario()
    {
        _projectRepoMock.Setup(r => r.ExistsAsync(_projectId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _templateRepoMock.Setup(r => r.GetByIdAsync(_templateId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TemplateEntity
            {
                TemplateId = _templateId,
                TenantId = _tenantId,
                Name = "Test Template",
                DocumentType = "narrative",
                CurrentVersion = "1.0",
                IsActive = true
            });

        _templateRepoMock.Setup(r => r.GetLatestPublishedVersionAsync(_templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TemplateVersionEntity
            {
                TemplateVersionId = Guid.NewGuid(),
                TemplateId = _templateId,
                TemplateVersion = "1.0",
                IsPublished = true,
                CreatedAt = DateTime.UtcNow
            });

        _sourceRepoMock.Setup(r => r.ResolveSourceEntityTypesAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), _projectId, _tenantId, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [_sourceId1] = "artifact" });
    }

    [Fact]
    public async Task GenerateDocument_ValidRequest_ReturnsDocumentArtifact()
    {
        SetupValidScenario();
        var request = CreateValidRequest();

        var result = await _service.GenerateDocumentAsync(
            _tenantId, _projectId, _userId, "corr-1", "key-1", request);

        Assert.NotNull(result);
        Assert.Equal("narrative", result.DocumentType);
        Assert.Equal("pdf", result.Format);
        Assert.Equal(_templateId.ToString(), result.TemplateId);
        Assert.Equal("1.0", result.TemplateVersion);
        Assert.False(result.BrandingApplied);
        Assert.False(result.WatermarkApplied);
        Assert.Equal("v1.0-1.0", result.FooterVersionText);
        Assert.Equal("draft", result.ReviewStatus);
        Assert.NotEmpty(result.ChecksumSha256);
        Assert.NotEmpty(result.StorageUri);
        Assert.Equal(1, result.Version);
    }

    [Fact]
    public async Task GenerateDocument_OpaqueIdentifiers_AreAccepted()
    {
        var opaqueTemplateId = "tmpl-001";
        var opaqueSourceId = "src-001";
        var normalizedSourceId = OpaqueIdentifier.TryNormalize(opaqueSourceId, "source", out var sourceGuid) ? sourceGuid : Guid.Empty;

        _projectRepoMock.Setup(r => r.ExistsAsync(_projectId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _templateRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TemplateEntity
            {
                TemplateId = Guid.NewGuid(),
                TenantId = _tenantId,
                Name = "Opaque Template",
                DocumentType = "narrative",
                CurrentVersion = "1.0",
                IsActive = true
            });
        _templateRepoMock.Setup(r => r.GetLatestPublishedVersionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TemplateVersionEntity
            {
                TemplateVersionId = Guid.NewGuid(),
                TemplateId = Guid.NewGuid(),
                TemplateVersion = "1.0",
                IsPublished = true,
                CreatedAt = DateTime.UtcNow
            });
        _sourceRepoMock.Setup(r => r.ResolveSourceEntityTypesAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), _projectId, _tenantId, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [normalizedSourceId] = "artifact" });

        var request = new GenerateDocumentRequestDto
        {
            DocumentType = "narrative",
            Format = "pdf",
            TemplateId = opaqueTemplateId,
            IncludeBranding = false,
            SourceIds = new List<string> { opaqueSourceId }
        };

        var result = await _service.GenerateDocumentAsync(
            _tenantId, _projectId, _userId, "corr-opaque", "key-opaque", request);

        Assert.Equal("narrative", result.DocumentType);
        Assert.Equal(opaqueTemplateId, request.TemplateId);
        Assert.Equal("draft", result.ReviewStatus);
    }

    [Fact]
    public async Task GenerateDocument_EmptyDocumentType_ThrowsArgumentException()
    {
        var request = CreateValidRequest();
        request.DocumentType = "";

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.GenerateDocumentAsync(_tenantId, _projectId, _userId, "corr-1", "key-1", request));
        Assert.Contains("documentType is required", ex.Message);
    }

    [Fact]
    public async Task GenerateDocument_InvalidDocumentType_ThrowsArgumentException()
    {
        var request = CreateValidRequest();
        request.DocumentType = "invalid";

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.GenerateDocumentAsync(_tenantId, _projectId, _userId, "corr-1", "key-1", request));
        Assert.Contains("documentType must be one of", ex.Message);
    }

    [Fact]
    public async Task GenerateDocument_InvalidFormat_ThrowsArgumentException()
    {
        var request = CreateValidRequest();
        request.Format = "txt";

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.GenerateDocumentAsync(_tenantId, _projectId, _userId, "corr-1", "key-1", request));
        Assert.Contains("format must be one of", ex.Message);
    }

    [Fact]
    public async Task GenerateDocument_EmptySourceIds_ThrowsArgumentException()
    {
        var request = CreateValidRequest();
        request.SourceIds = new List<string>();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.GenerateDocumentAsync(_tenantId, _projectId, _userId, "corr-1", "key-1", request));
        Assert.Contains("sourceIds must contain at least one", ex.Message);
    }

    [Fact]
    public async Task GenerateDocument_SourceIdsContainsEmptyIdentifier_ThrowsArgumentException()
    {
        var request = CreateValidRequest();
        request.SourceIds = new List<string> { _sourceId1.ToString(), " " };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.GenerateDocumentAsync(_tenantId, _projectId, _userId, "corr-1", "key-1", request));
        Assert.Contains("sourceIds must not contain empty identifiers", ex.Message);
    }

    [Fact]
    public async Task GenerateDocument_WatermarkTooLong_ThrowsArgumentException()
    {
        var request = CreateValidRequest();
        request.WatermarkText = new string('A', 201);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.GenerateDocumentAsync(_tenantId, _projectId, _userId, "corr-1", "key-1", request));
        Assert.Contains("watermarkText exceeds", ex.Message);
    }

    [Fact]
    public async Task GenerateDocument_ProjectNotFound_ThrowsKeyNotFoundException()
    {
        _projectRepoMock.Setup(r => r.ExistsAsync(_projectId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var request = CreateValidRequest();

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.GenerateDocumentAsync(_tenantId, _projectId, _userId, "corr-1", "key-1", request));
        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public async Task GenerateDocument_EmptyProjectId_ThrowsArgumentException()
    {
        var request = CreateValidRequest();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.GenerateDocumentAsync(_tenantId, Guid.Empty, _userId, "corr-1", "key-empty-project", request));
        Assert.Equal("projectId must be a non-empty identifier.", ex.Message);
    }

    [Fact]
    public async Task GenerateDocument_TemplateNotFound_ThrowsArgumentException()
    {
        _projectRepoMock.Setup(r => r.ExistsAsync(_projectId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _templateRepoMock.Setup(r => r.GetByIdAsync(_templateId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TemplateEntity?)null);
        var request = CreateValidRequest();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.GenerateDocumentAsync(_tenantId, _projectId, _userId, "corr-1", "key-1", request));
        Assert.Contains("does not exist", ex.Message);
    }

    [Fact]
    public async Task GenerateDocument_InactiveTemplate_ThrowsArgumentException()
    {
        _projectRepoMock.Setup(r => r.ExistsAsync(_projectId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _templateRepoMock.Setup(r => r.GetByIdAsync(_templateId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TemplateEntity { TemplateId = _templateId, IsActive = false, DocumentType = "narrative" });
        var request = CreateValidRequest();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.GenerateDocumentAsync(_tenantId, _projectId, _userId, "corr-1", "key-1", request));
        Assert.Contains("does not exist, is inactive", ex.Message);
    }

    [Fact]
    public async Task GenerateDocument_TemplateMismatchDocumentType_ThrowsArgumentException()
    {
        _projectRepoMock.Setup(r => r.ExistsAsync(_projectId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _templateRepoMock.Setup(r => r.GetByIdAsync(_templateId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TemplateEntity { TemplateId = _templateId, IsActive = true, DocumentType = "report" });
        var request = CreateValidRequest();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.GenerateDocumentAsync(_tenantId, _projectId, _userId, "corr-1", "key-1", request));
        Assert.Contains("does not support document type", ex.Message);
    }

    [Fact]
    public async Task GenerateDocument_TemplateUnsupportedFormat_ThrowsArgumentException()
    {
        _projectRepoMock.Setup(r => r.ExistsAsync(_projectId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _templateRepoMock.Setup(r => r.GetByIdAsync(_templateId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TemplateEntity
            {
                TemplateId = _templateId,
                IsActive = true,
                DocumentType = "narrative",
                SupportedFormatsCsv = "pdf,docx"
            });
        var request = CreateValidRequest();
        request.Format = "xlsx";

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.GenerateDocumentAsync(_tenantId, _projectId, _userId, "corr-1", "key-format", request));
        Assert.Contains("does not support format", ex.Message);
    }

    [Fact]
    public async Task GenerateDocument_NoPublishedVersion_ThrowsArgumentException()
    {
        _projectRepoMock.Setup(r => r.ExistsAsync(_projectId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _templateRepoMock.Setup(r => r.GetByIdAsync(_templateId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TemplateEntity { TemplateId = _templateId, IsActive = true, DocumentType = "narrative" });
        _templateRepoMock.Setup(r => r.GetLatestPublishedVersionAsync(_templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TemplateVersionEntity?)null);
        var request = CreateValidRequest();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.GenerateDocumentAsync(_tenantId, _projectId, _userId, "corr-1", "key-1", request));
        Assert.Contains("No published version", ex.Message);
    }

    [Fact]
    public async Task GenerateDocument_InvalidSourceId_ThrowsArgumentException()
    {
        _projectRepoMock.Setup(r => r.ExistsAsync(_projectId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _templateRepoMock.Setup(r => r.GetByIdAsync(_templateId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TemplateEntity { TemplateId = _templateId, IsActive = true, DocumentType = "narrative" });
        _templateRepoMock.Setup(r => r.GetLatestPublishedVersionAsync(_templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TemplateVersionEntity { TemplateVersion = "1.0", IsPublished = true });
        _sourceRepoMock.Setup(r => r.ResolveSourceEntityTypesAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), _projectId, _tenantId, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>());
        var request = CreateValidRequest();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.GenerateDocumentAsync(_tenantId, _projectId, _userId, "corr-1", "key-1", request));
        Assert.Contains("invalid, inaccessible, or outside", ex.Message);
    }

    [Fact]
    public async Task GenerateDocument_UnsupportedResolvedSourceType_ThrowsArgumentException()
    {
        _projectRepoMock.Setup(r => r.ExistsAsync(_projectId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _templateRepoMock.Setup(r => r.GetByIdAsync(_templateId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TemplateEntity { TemplateId = _templateId, IsActive = true, DocumentType = "narrative" });
        _templateRepoMock.Setup(r => r.GetLatestPublishedVersionAsync(_templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TemplateVersionEntity { TemplateVersion = "1.0", IsPublished = true });
        _sourceRepoMock.Setup(r => r.ResolveSourceEntityTypesAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), _projectId, _tenantId, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [_sourceId1] = "unknownType" });
        var request = CreateValidRequest();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.GenerateDocumentAsync(_tenantId, _projectId, _userId, "corr-1", "key-unsupported", request));
        Assert.Contains("unsupported source type", ex.Message);
    }

    [Fact]
    public async Task GenerateDocument_WithBrandingAvailable_BrandingApplied()
    {
        SetupValidScenario();
        _brandingRepoMock.Setup(r => r.GetByTenantIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BrandingAssetEntity
            {
                BrandingAssetId = Guid.NewGuid(),
                TenantId = _tenantId,
                LogoStorageUri = "nas://logo.png",
                ColorsJson = "{\"primary\":\"#000000\"}",
                Status = "active"
            });
        var request = CreateValidRequest();
        request.IncludeBranding = true;

        var result = await _service.GenerateDocumentAsync(
            _tenantId, _projectId, _userId, "corr-1", "key-1", request);

        Assert.True(result.BrandingApplied);
    }

    [Fact]
    public async Task GenerateDocument_WithBrandingNoAssets_BrandingNotApplied()
    {
        SetupValidScenario();
        _brandingRepoMock.Setup(r => r.GetByTenantIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BrandingAssetEntity?)null);
        var request = CreateValidRequest();
        request.IncludeBranding = true;

        var result = await _service.GenerateDocumentAsync(
            _tenantId, _projectId, _userId, "corr-1", "key-1", request);

        Assert.False(result.BrandingApplied);
    }

    [Fact]
    public async Task GenerateDocument_SameIdempotencyKey_UsesDeterministicDocumentId()
    {
        SetupValidScenario();
        var request = CreateValidRequest();
        var persistedIds = new List<Guid>();

        _docArtifactRepoMock.Setup(r => r.CreateAsync(
                It.IsAny<DocumentArtifactEntity>(), It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .Callback<DocumentArtifactEntity, IDbConnection, IDbTransaction, CancellationToken>((e, _, _, _) => persistedIds.Add(e.DocumentArtifactId))
            .Returns(Task.CompletedTask);

        await _service.GenerateDocumentAsync(_tenantId, _projectId, _userId, "corr-1", "idem-stable", request);
        await _service.GenerateDocumentAsync(_tenantId, _projectId, _userId, "corr-2", "idem-stable", request);

        Assert.Equal(2, persistedIds.Count);
        Assert.Equal(persistedIds[0], persistedIds[1]);
    }

    [Fact]
    public async Task GenerateDocument_WithWatermark_WatermarkApplied()
    {
        SetupValidScenario();
        var request = CreateValidRequest();
        request.WatermarkText = "Draft";

        var result = await _service.GenerateDocumentAsync(
            _tenantId, _projectId, _userId, "corr-1", "key-1", request);

        Assert.True(result.WatermarkApplied);
    }

    [Fact]
    public async Task GenerateDocument_WithoutWatermark_WatermarkNotApplied()
    {
        SetupValidScenario();
        var request = CreateValidRequest();
        request.WatermarkText = null;

        var result = await _service.GenerateDocumentAsync(
            _tenantId, _projectId, _userId, "corr-1", "key-1", request);

        Assert.False(result.WatermarkApplied);
    }

    [Fact]
    public async Task GenerateDocument_JsonFormat_DoesNotSetFooterVersionText()
    {
        SetupValidScenario();
        var request = CreateValidRequest();
        request.Format = "json";

        var result = await _service.GenerateDocumentAsync(
            _tenantId, _projectId, _userId, "corr-1", "key-json", request);

        Assert.Equal(string.Empty, result.FooterVersionText);
    }

    [Fact]
    public async Task GenerateDocument_SameInput_ProducesDeterministicChecksum()
    {
        SetupValidScenario();
        var request = CreateValidRequest();

        var first = await _service.GenerateDocumentAsync(
            _tenantId, _projectId, _userId, "corr-1", "key-checksum-1", request);
        var second = await _service.GenerateDocumentAsync(
            _tenantId, _projectId, _userId, "corr-1", "key-checksum-2", request);

        Assert.Equal(first.ChecksumSha256, second.ChecksumSha256);
    }

    [Fact]
    public async Task GenerateDocument_ChecksumMatchesRenderedContentSha256()
    {
        SetupValidScenario();
        var request = CreateValidRequest();

        var result = await _service.GenerateDocumentAsync(
            _tenantId, _projectId, _userId, "corr-checksum", "key-checksum-content", request);

        Assert.NotNull(_lastStoredContent);
        var expectedChecksum = DocumentRenderRuntime.ComputeChecksumSha256(_lastStoredContent!);
        Assert.Equal(expectedChecksum, result.ChecksumSha256);
    }

    [Fact]
    public async Task GenerateDocument_ChecksumIsLowerHexSha256()
    {
        SetupValidScenario();
        var request = CreateValidRequest();

        var result = await _service.GenerateDocumentAsync(
            _tenantId, _projectId, _userId, "corr-checksum-format", "key-checksum-format", request);

        Assert.Matches("^[0-9a-f]{64}$", result.ChecksumSha256);
    }

    [Fact]
    public async Task GenerateDocument_IdempotentReplay_ReturnsCachedResponse()
    {
        var cachedResponse = new DocumentArtifactResponseDto
        {
            Id = "doc-cached",
            TenantId = _tenantId.ToString(),
            DocumentType = "narrative",
            Format = "pdf",
            ReviewStatus = "draft"
        };

        var request = CreateValidRequest();
        // Need to compute the same hash as the service does
        var payload = $"{_projectId}|{request.DocumentType}|{request.Format}|{request.TemplateId}|{request.IncludeBranding}||{string.Join(",", request.SourceIds)}";
        var hashBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payload));
        var requestHash = Convert.ToHexStringLower(hashBytes);

        _idempotencyRepoMock.Setup(r => r.GetByKeyAsync("key-replay", _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdempotencyRecordEntity
            {
                IdempotencyKey = "key-replay",
                TenantId = _tenantId,
                RequestHash = requestHash,
                ResponseJson = JsonSerializer.Serialize(cachedResponse),
                StatusCode = 201
            });

        var result = await _service.GenerateDocumentAsync(
            _tenantId, _projectId, _userId, "corr-1", "key-replay", request);

        Assert.Equal("doc-cached", result.Id);
    }

    [Fact]
    public async Task GenerateDocument_IdempotencyKeyDifferentPayload_ThrowsInvalidOperationException()
    {
        _idempotencyRepoMock.Setup(r => r.GetByKeyAsync("key-dup", _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdempotencyRecordEntity
            {
                IdempotencyKey = "key-dup",
                TenantId = _tenantId,
                RequestHash = "different-hash",
                ResponseJson = "{}",
                StatusCode = 201
            });

        var request = CreateValidRequest();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.GenerateDocumentAsync(_tenantId, _projectId, _userId, "corr-1", "key-dup", request));
        Assert.Contains("Idempotency-Key", ex.Message);
    }

    [Theory]
    [InlineData("narrative")]
    [InlineData("calculator")]
    [InlineData("simulationSummary")]
    [InlineData("formReadyData")]
    [InlineData("scorecard")]
    [InlineData("checklist")]
    [InlineData("report")]
    public async Task GenerateDocument_AllSupportedDocumentTypes_Accepted(string docType)
    {
        SetupValidScenario();
        _templateRepoMock.Setup(r => r.GetByIdAsync(_templateId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TemplateEntity { TemplateId = _templateId, IsActive = true, DocumentType = docType });
        var request = CreateValidRequest();
        request.DocumentType = docType;

        var result = await _service.GenerateDocumentAsync(
            _tenantId, _projectId, _userId, "corr-1", $"key-{docType}", request);

        Assert.Equal(docType, result.DocumentType);
    }

    [Theory]
    [InlineData("pdf")]
    [InlineData("docx")]
    [InlineData("xlsx")]
    [InlineData("json")]
    [InlineData("pptx")]
    public async Task GenerateDocument_AllSupportedFormats_Accepted(string format)
    {
        SetupValidScenario();
        var request = CreateValidRequest();
        request.Format = format;

        var result = await _service.GenerateDocumentAsync(
            _tenantId, _projectId, _userId, "corr-1", $"key-{format}", request);

        Assert.Equal(format, result.Format);
    }

    [Fact]
    public async Task GenerateDocument_PersistsDocumentArtifactAndSources()
    {
        SetupValidScenario();
        var request = CreateValidRequest();

        await _service.GenerateDocumentAsync(
            _tenantId, _projectId, _userId, "corr-1", "key-persist", request);

        _docArtifactRepoMock.Verify(r => r.CreateAsync(
            It.IsAny<DocumentArtifactEntity>(),
            It.IsAny<IDbConnection>(),
            It.IsAny<IDbTransaction>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _docSourceRepoMock.Verify(r => r.CreateAsync(
            It.IsAny<DocumentSourceEntity>(),
            It.IsAny<IDbConnection>(),
            It.IsAny<IDbTransaction>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _auditRepoMock.Verify(r => r.InsertAuditLogAsync(
            It.IsAny<AuditLogEntity>(),
            It.IsAny<IDbConnection>(),
            It.IsAny<IDbTransaction>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateDocument_ResolvesSourcesUsingActorAuthorizationScope()
    {
        SetupValidScenario();
        var request = CreateValidRequest();

        await _service.GenerateDocumentAsync(
            _tenantId, _projectId, _userId, "corr-authz", "key-authz", request);

        _sourceRepoMock.Verify(r => r.ResolveSourceEntityTypesAsync(
            It.IsAny<IReadOnlyCollection<Guid>>(),
            _projectId,
            _tenantId,
            _userId,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
