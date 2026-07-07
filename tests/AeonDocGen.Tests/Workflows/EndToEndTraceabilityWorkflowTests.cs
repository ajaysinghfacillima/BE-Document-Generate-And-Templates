using System.Data;
using AeonDocGen.Core.DTOs;
using AeonDocGen.Core.Interfaces;
using AeonDocGen.Core.Models;
using AeonDocGen.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace AeonDocGen.Tests.Workflows;

public class EndToEndTraceabilityWorkflowTests
{
    [Fact]
    public async Task RequiredFlow_GenerateThenReview_IsTraceableAcrossArtifactsAndAudit()
    {
        var tenantId = Guid.Parse("00000000-0000-0000-0000-0000000000AA");
        var projectId = Guid.Parse("00000000-0000-0000-0000-0000000000BB");
        var actorId = Guid.Parse("00000000-0000-0000-0000-0000000000CC");
        var templateId = Guid.Parse("00000000-0000-0000-0000-0000000000DD");
        var sourceId = Guid.Parse("00000000-0000-0000-0000-0000000000EE");

        var projectRepo = new Mock<IProjectRepository>();
        projectRepo.Setup(r => r.ExistsAsync(projectId, tenantId, It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        projectRepo.Setup(r => r.ExistsAsync(projectId, tenantId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var templateRepo = new Mock<ITemplateResolutionRepository>();
        templateRepo.Setup(r => r.GetByIdAsync(templateId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TemplateEntity { TemplateId = templateId, TenantId = tenantId, IsActive = true, DocumentType = "narrative" });
        templateRepo.Setup(r => r.GetLatestPublishedVersionAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TemplateVersionEntity { TemplateVersionId = Guid.NewGuid(), TemplateId = templateId, TemplateVersion = "1.0", IsPublished = true });

        var sourceRepo = new Mock<ISourceEntityRepository>();
        sourceRepo.Setup(r => r.ResolveSourceEntityTypesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), projectId, tenantId, actorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [sourceId] = "artifact" });

        var brandingRepo = new Mock<IBrandingAssetRepository>();
        var docSourceRepo = new Mock<IDocumentSourceRepository>();
        var reviewEventRepo = new Mock<IDocumentReviewEventRepository>();
        var auditRepo = new Mock<IAuditLogRepository>();
        var idempotencyRepo = new Mock<IIdempotencyRepository>();
        var createdSources = new List<DocumentSourceEntity>();
        var createdReviewEvents = new List<DocumentReviewEventEntity>();
        var createdAuditLogs = new List<AuditLogEntity>();
        var storageClient = new Mock<IDocumentStorageClient>();
        storageClient.Setup(s => s.StoreFileAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string path, byte[] _, CancellationToken _) => $"nas://{path}");

        var connFactory = new Mock<IDbConnectionFactory>();
        var connection = new Mock<IDbConnection>();
        var transaction = new Mock<IDbTransaction>();
        connection.Setup(c => c.BeginTransaction()).Returns(transaction.Object);
        connFactory.Setup(f => f.CreateConnection()).Returns(connection.Object);

        var persisted = new Dictionary<Guid, DocumentArtifactEntity>();
        var docArtifactRepo = new Mock<IDocumentArtifactRepository>();
        docArtifactRepo.Setup(r => r.CreateAsync(It.IsAny<DocumentArtifactEntity>(), It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .Callback<DocumentArtifactEntity, IDbConnection, IDbTransaction, CancellationToken>((e, _, _, _) => persisted[e.DocumentArtifactId] = e)
            .Returns(Task.CompletedTask);
        docArtifactRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), projectId, tenantId, It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, Guid _, Guid _, IDbConnection _, IDbTransaction _, CancellationToken _) =>
                persisted.TryGetValue(id, out var entity) ? entity : null);
        docArtifactRepo.Setup(r => r.UpdateReviewStatusAsync(It.IsAny<DocumentArtifactEntity>(), It.IsAny<string>(), It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        docSourceRepo.Setup(r => r.CreateAsync(It.IsAny<DocumentSourceEntity>(), It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .Callback<DocumentSourceEntity, IDbConnection, IDbTransaction, CancellationToken>((e, _, _, _) => createdSources.Add(e))
            .Returns(Task.CompletedTask);
        reviewEventRepo.Setup(r => r.CreateAsync(It.IsAny<DocumentReviewEventEntity>(), It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .Callback<DocumentReviewEventEntity, IDbConnection, IDbTransaction, CancellationToken>((e, _, _, _) => createdReviewEvents.Add(e))
            .Returns(Task.CompletedTask);
        auditRepo.Setup(r => r.InsertAuditLogAsync(It.IsAny<AuditLogEntity>(), It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .Callback<AuditLogEntity, IDbConnection, IDbTransaction, CancellationToken>((e, _, _, _) => createdAuditLogs.Add(e))
            .Returns(Task.CompletedTask);

        var generationService = new DocumentGenerationService(
            projectRepo.Object, templateRepo.Object, sourceRepo.Object, brandingRepo.Object, docArtifactRepo.Object,
            docSourceRepo.Object, auditRepo.Object, idempotencyRepo.Object, storageClient.Object, connFactory.Object,
            Options.Create(new DocumentGenerationSettings { FooterVersionFormat = "v1.0", MaxWatermarkLength = 100, EnableInlineRenderWorkerFallback = true }),
            new Mock<ILogger<DocumentGenerationService>>().Object);

        var reviewService = new DocumentReviewService(
            projectRepo.Object, docArtifactRepo.Object, reviewEventRepo.Object, auditRepo.Object, idempotencyRepo.Object,
            connFactory.Object, new Mock<ILogger<DocumentReviewService>>().Object);

        var generated = await generationService.GenerateDocumentAsync(
            tenantId,
            projectId,
            actorId,
            "corr-e2e",
            "idem-generate-e2e",
            new GenerateDocumentRequestDto
            {
                DocumentType = "narrative",
                Format = "pdf",
                TemplateId = templateId.ToString(),
                IncludeBranding = false,
                SourceIds = new List<string> { sourceId.ToString() }
            },
            CancellationToken.None);

        var reviewed = await reviewService.ReviewDocumentAsync(
            tenantId,
            projectId,
            Guid.Parse(generated.Id),
            actorId,
            "corr-e2e",
            "idem-review-e2e",
            generated.Etag,
            new DocumentReviewRequestDto { Action = "startReview", Comments = "traceable e2e transition" },
            CancellationToken.None);

        var approved = await reviewService.ReviewDocumentAsync(
            tenantId,
            projectId,
            Guid.Parse(generated.Id),
            actorId,
            "corr-e2e",
            "idem-review-e2e-approve",
            reviewed.Etag,
            new DocumentReviewRequestDto { Action = "approve", Comments = "traceable approval" },
            CancellationToken.None);

        Assert.Equal("draft", generated.ReviewStatus);
        Assert.Equal("inReview", reviewed.ReviewStatus);
        Assert.Equal("approved", approved.ReviewStatus);
        Assert.Equal(generated.ProjectId, reviewed.ProjectId);
        Assert.NotEqual(generated.Etag, reviewed.Etag);

        Assert.Single(createdSources);
        Assert.Equal(sourceId, createdSources[0].SourceEntityId);
        Assert.Equal("artifact", createdSources[0].SourceEntityType);

        Assert.Equal(2, createdReviewEvents.Count);
        Assert.Equal("startReview", createdReviewEvents[0].Action);
        Assert.Equal("approve", createdReviewEvents[1].Action);
        Assert.Equal(actorId, createdReviewEvents[1].ActorUserId);

        Assert.Equal(3, createdAuditLogs.Count);
        Assert.All(createdAuditLogs, log =>
        {
            Assert.Equal(tenantId, log.TenantId);
            Assert.Equal("corr-e2e", log.CorrelationId);
            Assert.Equal("DocumentArtifact", log.ResourceType);
            Assert.Equal("success", log.Outcome);
        });
        Assert.Contains(createdAuditLogs, log => log.Action == "documents.generate");
        Assert.Contains(createdAuditLogs, log => log.Action == "documents.review.startReview");
        Assert.Contains(createdAuditLogs, log => log.Action == "documents.review.approve");
    }

    [Fact]
    public async Task RequiredFlows_TemplateBrandingGenerationReview_AreTraceableEndToEnd()
    {
        var tenantId = Guid.Parse("00000000-0000-0000-0000-0000000001AA");
        var projectId = Guid.Parse("00000000-0000-0000-0000-0000000001BB");
        var actorId = Guid.Parse("00000000-0000-0000-0000-0000000001CC");
        var templateId = Guid.Parse("00000000-0000-0000-0000-0000000001DD");
        var sourceId = Guid.Parse("00000000-0000-0000-0000-0000000001EE");

        var projectRepo = new Mock<IProjectRepository>();
        projectRepo.Setup(r => r.ExistsAsync(projectId, tenantId, It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        projectRepo.Setup(r => r.ExistsAsync(projectId, tenantId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var templateQueryRepo = new Mock<ITemplateRepository>();
        templateQueryRepo.Setup(r => r.GetTemplatesByTenantIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TemplateEntity>
            {
                new() { TemplateId = templateId, TenantId = tenantId, Name = "LEED Narrative", DocumentType = "narrative", CurrentVersion = "1.0", IsActive = true }
            });
        templateQueryRepo.Setup(r => r.GetTemplateVersionsByTemplateIdsAsync(tenantId, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TemplateVersionEntity>
            {
                new() { TemplateVersionId = Guid.NewGuid(), TemplateId = templateId, TemplateVersion = "1.1", IsPublished = true, CreatedAt = DateTime.UtcNow }
            });

        var templateResolutionRepo = new Mock<ITemplateResolutionRepository>();
        templateResolutionRepo.Setup(r => r.GetByIdAsync(templateId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TemplateEntity { TemplateId = templateId, TenantId = tenantId, IsActive = true, DocumentType = "narrative", CurrentVersion = "1.0" });
        templateResolutionRepo.Setup(r => r.GetLatestPublishedVersionAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TemplateVersionEntity { TemplateVersionId = Guid.NewGuid(), TemplateId = templateId, TemplateVersion = "1.1", IsPublished = true });

        var sourceRepo = new Mock<ISourceEntityRepository>();
        sourceRepo.Setup(r => r.ResolveSourceEntityTypesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), projectId, tenantId, actorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [sourceId] = "artifact" });

        var brandingRepo = new Mock<IBrandingAssetRepository>();
        BrandingAssetEntity? brandingState = null;
        brandingRepo.Setup(r => r.GetByTenantIdAsync(tenantId, It.IsAny<CancellationToken>())).ReturnsAsync(() => brandingState);
        brandingRepo.Setup(r => r.CreateAsync(It.IsAny<BrandingAssetEntity>(), It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .Callback<BrandingAssetEntity, IDbConnection, IDbTransaction, CancellationToken>((e, _, _, _) => brandingState = e)
            .Returns(Task.CompletedTask);
        brandingRepo.Setup(r => r.UpdateAsync(It.IsAny<BrandingAssetEntity>(), It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .Callback<BrandingAssetEntity, IDbConnection, IDbTransaction, CancellationToken>((e, _, _, _) => brandingState = e)
            .Returns(Task.CompletedTask);

        var docSourceRepo = new Mock<IDocumentSourceRepository>();
        var reviewEventRepo = new Mock<IDocumentReviewEventRepository>();
        var auditRepo = new Mock<IAuditLogRepository>();
        var idempotencyRepo = new Mock<IIdempotencyRepository>();
        var createdAuditLogs = new List<AuditLogEntity>();
        var storageClient = new Mock<IDocumentStorageClient>();
        storageClient.Setup(s => s.StoreFileAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string path, byte[] _, CancellationToken _) => $"nas://{path}");
        var brandingStorage = new Mock<IBrandingStorageClient>();
        brandingStorage.Setup(s => s.StoreFileAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string path, byte[] _, CancellationToken _) => $"nas://{path}");
        var malware = new Mock<IMalwareScannerClient>();
        malware.Setup(m => m.IsSafeAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var connFactory = new Mock<IDbConnectionFactory>();
        var connection = new Mock<IDbConnection>();
        var transaction = new Mock<IDbTransaction>();
        connection.Setup(c => c.BeginTransaction()).Returns(transaction.Object);
        connFactory.Setup(f => f.CreateConnection()).Returns(connection.Object);

        var persisted = new Dictionary<Guid, DocumentArtifactEntity>();
        var docArtifactRepo = new Mock<IDocumentArtifactRepository>();
        docArtifactRepo.Setup(r => r.CreateAsync(It.IsAny<DocumentArtifactEntity>(), It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .Callback<DocumentArtifactEntity, IDbConnection, IDbTransaction, CancellationToken>((e, _, _, _) => persisted[e.DocumentArtifactId] = e)
            .Returns(Task.CompletedTask);
        docArtifactRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), projectId, tenantId, It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, Guid _, Guid _, IDbConnection _, IDbTransaction _, CancellationToken _) => persisted.TryGetValue(id, out var entity) ? entity : null);
        docArtifactRepo.Setup(r => r.UpdateReviewStatusAsync(It.IsAny<DocumentArtifactEntity>(), It.IsAny<string>(), It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        reviewEventRepo.Setup(r => r.CreateAsync(It.IsAny<DocumentReviewEventEntity>(), It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        docSourceRepo.Setup(r => r.CreateAsync(It.IsAny<DocumentSourceEntity>(), It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        auditRepo.Setup(r => r.InsertAuditLogAsync(It.IsAny<AuditLogEntity>(), It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .Callback<AuditLogEntity, IDbConnection, IDbTransaction, CancellationToken>((e, _, _, _) => createdAuditLogs.Add(e))
            .Returns(Task.CompletedTask);
        auditRepo.Setup(r => r.InsertAuditLogAsync(It.IsAny<AuditLogEntity>(), It.IsAny<CancellationToken>()))
            .Callback<AuditLogEntity, CancellationToken>((e, _) => createdAuditLogs.Add(e))
            .Returns(Task.CompletedTask);

        var templateService = new AdminTemplateService(
            templateQueryRepo.Object,
            auditRepo.Object,
            new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()),
            new Mock<ILogger<AdminTemplateService>>().Object);
        var brandingService = new BrandingAssetService(
            brandingRepo.Object, auditRepo.Object, idempotencyRepo.Object, brandingStorage.Object, malware.Object, connFactory.Object,
            Options.Create(new BrandingUploadSettings
            {
                AllowedLogoMimeTypes = new[] { "image/png", "image/jpeg", "image/svg+xml" },
                AllowedFontExtensions = new[] { ".ttf", ".otf", ".woff", ".woff2" },
                MaxLogoSizeBytes = 5 * 1024 * 1024,
                MaxFontsZipSizeBytes = 20 * 1024 * 1024,
                MalwareScanEnabled = true
            }),
            new Mock<ILogger<BrandingAssetService>>().Object);
        var generationService = new DocumentGenerationService(
            projectRepo.Object, templateResolutionRepo.Object, sourceRepo.Object, brandingRepo.Object, docArtifactRepo.Object,
            docSourceRepo.Object, auditRepo.Object, idempotencyRepo.Object, storageClient.Object, connFactory.Object,
            Options.Create(new DocumentGenerationSettings { FooterVersionFormat = "v1.0", MaxWatermarkLength = 100, EnableInlineRenderWorkerFallback = true }),
            new Mock<ILogger<DocumentGenerationService>>().Object);
        var reviewService = new DocumentReviewService(
            projectRepo.Object, docArtifactRepo.Object, reviewEventRepo.Object, auditRepo.Object, idempotencyRepo.Object,
            connFactory.Object, new Mock<ILogger<DocumentReviewService>>().Object);

        var templates = await templateService.ListTemplatesAsync(tenantId, actorId, "corr-required", CancellationToken.None);
        var branding = await brandingService.UploadBrandingAssetsAsync(
            tenantId, actorId, "corr-required", "idem-branding-required",
            new BrandingUploadInput { ColorsJson = "{\"primary\":\"#0F4C81\",\"secondary\":\"#7FB3D5\",\"accent\":\"#F4B400\",\"text\":\"#1F2937\",\"background\":\"#FFFFFF\"}" },
            CancellationToken.None);
        var generated = await generationService.GenerateDocumentAsync(
            tenantId, projectId, actorId, "corr-required", "idem-generate-required",
            new GenerateDocumentRequestDto
            {
                DocumentType = "narrative",
                Format = "pdf",
                TemplateId = templateId.ToString(),
                IncludeBranding = true,
                SourceIds = new List<string> { sourceId.ToString() }
            },
            CancellationToken.None);
        var reviewed = await reviewService.ReviewDocumentAsync(
            tenantId, projectId, Guid.Parse(generated.Id), actorId, "corr-required", "idem-review-required",
            generated.Etag, new DocumentReviewRequestDto { Action = "startReview", Comments = "review started" }, CancellationToken.None);

        Assert.Single(templates.Items);
        Assert.Equal("1.1", templates.Items[0].CurrentVersion);
        Assert.Equal("updated", branding.Status);
        Assert.True(generated.BrandingApplied);
        Assert.Equal("inReview", reviewed.ReviewStatus);

        Assert.Contains(createdAuditLogs, l => l.Action == "templates.list" && l.CorrelationId == "corr-required");
        Assert.Contains(createdAuditLogs, l => l.Action == "branding.assets.upload" && l.CorrelationId == "corr-required");
        Assert.Contains(createdAuditLogs, l => l.Action == "documents.generate" && l.CorrelationId == "corr-required");
        Assert.Contains(createdAuditLogs, l => l.Action == "documents.review.startReview" && l.CorrelationId == "corr-required");
    }
}
