using AeonDocGen.Core.Interfaces;
using AeonDocGen.Core.Models;
using AeonDocGen.Core.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace AeonDocGen.Tests;

public class AdminTemplateServiceTests
{
    private readonly Mock<ITemplateRepository> _templateRepoMock;
    private readonly Mock<IAuditLogRepository> _auditLogRepoMock;
    private readonly Mock<ILogger<AdminTemplateService>> _loggerMock;
    private readonly MemoryCache _cache;
    private readonly AdminTemplateService _service;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _actorUserId = Guid.NewGuid();
    private const string CorrelationId = "corr-test-001";

    public AdminTemplateServiceTests()
    {
        _templateRepoMock = new Mock<ITemplateRepository>();
        _auditLogRepoMock = new Mock<IAuditLogRepository>();
        _loggerMock = new Mock<ILogger<AdminTemplateService>>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _service = new AdminTemplateService(_templateRepoMock.Object, _auditLogRepoMock.Object, _cache, _loggerMock.Object);
    }

    [Fact]
    public async Task ListTemplatesAsync_ReturnsEmptyItems_WhenNoTemplatesExist()
    {
        _templateRepoMock.Setup(r => r.GetTemplatesByTenantIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TemplateEntity>());
        _templateRepoMock.Setup(r => r.GetTemplateVersionsByTemplateIdsAsync(_tenantId, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TemplateVersionEntity>());

        var result = await _service.ListTemplatesAsync(_tenantId, _actorUserId, CorrelationId);

        Assert.NotNull(result);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task ListTemplatesAsync_ReturnsSingleTemplate_WithVersionsAndDocTypes()
    {
        var templateId = Guid.NewGuid();
        var templates = new List<TemplateEntity>
        {
            new() { TemplateId = templateId, TenantId = _tenantId, Name = "LEED Narrative", DocumentType = "narrative", CurrentVersion = "2.0" }
        };
        var versions = new List<TemplateVersionEntity>
        {
            new() { TemplateVersionId = Guid.NewGuid(), TemplateId = templateId, TemplateVersion = "2.0", CreatedAt = DateTime.UtcNow },
            new() { TemplateVersionId = Guid.NewGuid(), TemplateId = templateId, TemplateVersion = "1.0", CreatedAt = DateTime.UtcNow.AddDays(-30) }
        };

        _templateRepoMock.Setup(r => r.GetTemplatesByTenantIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(templates);
        _templateRepoMock.Setup(r => r.GetTemplateVersionsByTemplateIdsAsync(_tenantId, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(versions);

        var result = await _service.ListTemplatesAsync(_tenantId, _actorUserId, CorrelationId);

        Assert.Single(result.Items);
        var item = result.Items[0];
        Assert.Equal(templateId.ToString(), item.Id);
        Assert.Equal("LEED Narrative", item.Name);
        Assert.Equal("2.0", item.CurrentVersion);
        Assert.Equal(2, item.Versions.Count);
        Assert.Equal("2.0", item.Versions[0]);
        Assert.Equal("1.0", item.Versions[1]);
        Assert.Single(item.DocumentTypes);
        Assert.Contains("narrative", item.DocumentTypes);
    }

    [Fact]
    public async Task ListTemplatesAsync_ReturnsMultipleTemplates_OrderedByName()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var templates = new List<TemplateEntity>
        {
            new() { TemplateId = id2, TenantId = _tenantId, Name = "Submission Package", DocumentType = "package", CurrentVersion = "1.1" },
            new() { TemplateId = id1, TenantId = _tenantId, Name = "LEED General Narrative", DocumentType = "narrative", CurrentVersion = "2.1" }
        };

        _templateRepoMock.Setup(r => r.GetTemplatesByTenantIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(templates);
        _templateRepoMock.Setup(r => r.GetTemplateVersionsByTemplateIdsAsync(_tenantId, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TemplateVersionEntity>());

        var result = await _service.ListTemplatesAsync(_tenantId, _actorUserId, CorrelationId);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal("LEED General Narrative", result.Items[0].Name);
        Assert.Equal("Submission Package", result.Items[1].Name);
    }

    [Fact]
    public async Task ListTemplatesAsync_UsesLatestVersionFromTemplateVersions_AsCurrentVersion()
    {
        var templateId = Guid.NewGuid();
        var templates = new List<TemplateEntity>
        {
            new() { TemplateId = templateId, TenantId = _tenantId, Name = "Test Template", DocumentType = "report", CurrentVersion = "1.0" }
        };
        var versions = new List<TemplateVersionEntity>
        {
            new() { TemplateVersionId = Guid.NewGuid(), TemplateId = templateId, TemplateVersion = "3.0", CreatedAt = DateTime.UtcNow },
            new() { TemplateVersionId = Guid.NewGuid(), TemplateId = templateId, TemplateVersion = "2.0", CreatedAt = DateTime.UtcNow.AddDays(-10) },
            new() { TemplateVersionId = Guid.NewGuid(), TemplateId = templateId, TemplateVersion = "1.0", CreatedAt = DateTime.UtcNow.AddDays(-20) }
        };

        _templateRepoMock.Setup(r => r.GetTemplatesByTenantIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(templates);
        _templateRepoMock.Setup(r => r.GetTemplateVersionsByTemplateIdsAsync(_tenantId, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(versions);

        var result = await _service.ListTemplatesAsync(_tenantId, _actorUserId, CorrelationId);

        Assert.Single(result.Items);
        Assert.Equal("3.0", result.Items[0].CurrentVersion);
        Assert.Equal(new List<string> { "3.0", "2.0", "1.0" }, result.Items[0].Versions);
    }

    [Fact]
    public async Task ListTemplatesAsync_PrefersLatestPublishedVersion_AsCurrentVersion()
    {
        var templateId = Guid.NewGuid();
        var templates = new List<TemplateEntity>
        {
            new() { TemplateId = templateId, TenantId = _tenantId, Name = "Published Template", DocumentType = "report", CurrentVersion = "1.0" }
        };
        var versions = new List<TemplateVersionEntity>
        {
            new() { TemplateVersionId = Guid.NewGuid(), TemplateId = templateId, TemplateVersion = "4.0-draft", IsPublished = false, CreatedAt = DateTime.UtcNow },
            new() { TemplateVersionId = Guid.NewGuid(), TemplateId = templateId, TemplateVersion = "3.0", IsPublished = true, CreatedAt = DateTime.UtcNow.AddDays(-1) }
        };

        _templateRepoMock.Setup(r => r.GetTemplatesByTenantIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(templates);
        _templateRepoMock.Setup(r => r.GetTemplateVersionsByTemplateIdsAsync(_tenantId, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(versions);

        var result = await _service.ListTemplatesAsync(_tenantId, _actorUserId, CorrelationId);

        Assert.Single(result.Items);
        Assert.Equal("3.0", result.Items[0].CurrentVersion);
    }

    [Fact]
    public async Task ListTemplatesAsync_CreatesAuditLogEntry()
    {
        _templateRepoMock.Setup(r => r.GetTemplatesByTenantIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TemplateEntity>());
        _templateRepoMock.Setup(r => r.GetTemplateVersionsByTemplateIdsAsync(_tenantId, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TemplateVersionEntity>());

        await _service.ListTemplatesAsync(_tenantId, _actorUserId, CorrelationId);

        _auditLogRepoMock.Verify(r => r.InsertAuditLogAsync(
            It.Is<AuditLogEntity>(a =>
                a.TenantId == _tenantId &&
                a.ActorUserId == _actorUserId &&
                a.Action == "templates.list" &&
                a.ResourceType == "Template" &&
                a.ScopeType == "tenant" &&
                a.ScopeId == _tenantId &&
                a.Outcome == "success" &&
                a.CorrelationId == CorrelationId &&
                !string.IsNullOrEmpty(a.ImmutableHash)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListTemplatesAsync_VersionsDescendByCreatedAt()
    {
        var templateId = Guid.NewGuid();
        var templates = new List<TemplateEntity>
        {
            new() { TemplateId = templateId, TenantId = _tenantId, Name = "T1", DocumentType = "narrative", CurrentVersion = "1.0" }
        };
        var versions = new List<TemplateVersionEntity>
        {
            new() { TemplateVersionId = Guid.NewGuid(), TemplateId = templateId, TemplateVersion = "1.0", CreatedAt = new DateTime(2026, 1, 1) },
            new() { TemplateVersionId = Guid.NewGuid(), TemplateId = templateId, TemplateVersion = "2.0", CreatedAt = new DateTime(2026, 6, 1) },
            new() { TemplateVersionId = Guid.NewGuid(), TemplateId = templateId, TemplateVersion = "1.5", CreatedAt = new DateTime(2026, 3, 1) }
        };

        _templateRepoMock.Setup(r => r.GetTemplatesByTenantIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(templates);
        _templateRepoMock.Setup(r => r.GetTemplateVersionsByTemplateIdsAsync(_tenantId, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(versions);

        var result = await _service.ListTemplatesAsync(_tenantId, _actorUserId, CorrelationId);

        Assert.Equal(new List<string> { "2.0", "1.5", "1.0" }, result.Items[0].Versions);
    }

    [Fact]
    public async Task ListTemplatesAsync_MultipleDocTypesForSameTemplate_AreGrouped()
    {
        var templateId = Guid.NewGuid();
        var templates = new List<TemplateEntity>
        {
            new() { TemplateId = templateId, TenantId = _tenantId, Name = "Multi Template", DocumentType = "report", CurrentVersion = "1.0" },
            new() { TemplateId = templateId, TenantId = _tenantId, Name = "Multi Template", DocumentType = "scorecard", CurrentVersion = "1.0" }
        };

        _templateRepoMock.Setup(r => r.GetTemplatesByTenantIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(templates);
        _templateRepoMock.Setup(r => r.GetTemplateVersionsByTemplateIdsAsync(_tenantId, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TemplateVersionEntity>());

        var result = await _service.ListTemplatesAsync(_tenantId, _actorUserId, CorrelationId);

        Assert.Single(result.Items);
        Assert.Equal(2, result.Items[0].DocumentTypes.Count);
        Assert.Contains("report", result.Items[0].DocumentTypes);
        Assert.Contains("scorecard", result.Items[0].DocumentTypes);
    }

    [Fact]
    public async Task ListTemplatesAsync_ReusesTenantScopedCacheWithinTtl()
    {
        var templateId = Guid.NewGuid();
        var templates = new List<TemplateEntity>
        {
            new() { TemplateId = templateId, TenantId = _tenantId, Name = "Cached Template", DocumentType = "narrative", CurrentVersion = "1.0" }
        };

        _templateRepoMock.Setup(r => r.GetTemplatesByTenantIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(templates);
        _templateRepoMock.Setup(r => r.GetTemplateVersionsByTemplateIdsAsync(_tenantId, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TemplateVersionEntity>());

        var first = await _service.ListTemplatesAsync(_tenantId, _actorUserId, CorrelationId);
        var second = await _service.ListTemplatesAsync(_tenantId, _actorUserId, CorrelationId);

        Assert.Single(first.Items);
        Assert.Single(second.Items);
        _templateRepoMock.Verify(r => r.GetTemplatesByTenantIdAsync(_tenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListTemplatesAsync_DoesNotThrowForLargeTenantCatalog()
    {
        _templateRepoMock.Setup(r => r.GetTemplatesByTenantIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Range(1, 250)
                .Select(i => new TemplateEntity
                {
                    TemplateId = Guid.NewGuid(),
                    TenantId = _tenantId,
                    Name = $"Template {i:D3}",
                    DocumentType = "narrative",
                    CurrentVersion = "1.0"
                })
                .ToList());
        _templateRepoMock.Setup(r => r.GetTemplateVersionsByTemplateIdsAsync(_tenantId, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TemplateVersionEntity>());

        var result = await _service.ListTemplatesAsync(_tenantId, _actorUserId, CorrelationId);

        Assert.Equal(250, result.Items.Count);
    }
}
