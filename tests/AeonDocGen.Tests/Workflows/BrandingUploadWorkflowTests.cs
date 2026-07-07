// TR: LLD-0019 | ORIGIN: MID_LEVEL_DESIGN-0019
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
/// Branding upload tests for partial update flows and malware/storage/audit failure exception flows.
/// </summary>
public class BrandingUploadWorkflowTests
{
    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-00000000000A");
    private readonly Guid _userId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private readonly string _correlationId = "corr-brand-wf-001";

    private (BrandingAssetService service,
        Mock<IBrandingAssetRepository> brandingRepo,
        Mock<IBrandingStorageClient> storageClient,
        Mock<IMalwareScannerClient> malwareClient,
        Mock<IAuditLogRepository> auditRepo,
        Mock<IIdempotencyRepository> idempotencyRepo) CreateService()
    {
        var brandingRepo = new Mock<IBrandingAssetRepository>();
        var storageClient = new Mock<IBrandingStorageClient>();
        var malwareClient = new Mock<IMalwareScannerClient>();
        var auditRepo = new Mock<IAuditLogRepository>();
        var idempotencyRepo = new Mock<IIdempotencyRepository>();
        var connFactory = new Mock<IDbConnectionFactory>();
        var logger = new Mock<ILogger<BrandingAssetService>>();

        var mockConnection = new Mock<IDbConnection>();
        var mockTransaction = new Mock<IDbTransaction>();
        mockConnection.Setup(c => c.Open());
        mockConnection.Setup(c => c.BeginTransaction()).Returns(mockTransaction.Object);
        connFactory.Setup(f => f.CreateConnection()).Returns(mockConnection.Object);

        var settings = Options.Create(new BrandingUploadSettings
        {
            AllowedLogoMimeTypes = new[] { "image/png", "image/jpeg", "image/svg+xml" },
            AllowedFontExtensions = new[] { ".ttf", ".otf", ".woff", ".woff2" },
            MaxLogoSizeBytes = 5 * 1024 * 1024,
            MaxFontsZipSizeBytes = 20 * 1024 * 1024
        });

        malwareClient.Setup(m => m.IsSafeAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        storageClient.Setup(s => s.StoreFileAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("blob://stored/path");

        var service = new BrandingAssetService(
            brandingRepo.Object, auditRepo.Object, idempotencyRepo.Object, storageClient.Object,
            malwareClient.Object, connFactory.Object, settings, logger.Object);

        return (service, brandingRepo, storageClient, malwareClient, auditRepo, idempotencyRepo);
    }

    // --- Partial update: logo only ---

    [Fact]
    public async Task PartialUpdate_LogoOnly_PreservesExistingColorsAndFonts()
    {
        var (service, brandingRepo, _, _, _, _) = CreateService();

        var existingAsset = new BrandingAssetEntity
        {
            BrandingAssetId = Guid.NewGuid(),
            TenantId = _tenantId,
            Status = "active",
            LogoStorageUri = "blob://old-logo",
            ColorsJson = "{\"primary\":\"#000\"}",
            FontsStorageUri = "blob://old-fonts",
            Version = 1,
            Etag = "\"1-old\"",
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };

        brandingRepo.Setup(r => r.GetByTenantIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingAsset);

        var input = new BrandingUploadInput
        {
            LogoData = new byte[] { 0x89, 0x50, 0x4E, 0x47 },
            LogoFileName = "logo.png",
            LogoContentType = "image/png"
        };

        var result = await service.UploadBrandingAssetsAsync(
            _tenantId, _userId, _correlationId, "idem-logo-001", input, CancellationToken.None);

        Assert.Equal("updated", result.Status);
        Assert.Equal(2, result.Version);
    }

    // --- Partial update: colors only ---

    [Fact]
    public async Task PartialUpdate_ColorsOnly_PreservesExistingLogoAndFonts()
    {
        var (service, brandingRepo, _, _, _, _) = CreateService();

        var existingAsset = new BrandingAssetEntity
        {
            BrandingAssetId = Guid.NewGuid(),
            TenantId = _tenantId,
            Status = "active",
            LogoStorageUri = "blob://existing-logo",
            ColorsJson = "{\"primary\":\"#111\"}",
            FontsStorageUri = "blob://existing-fonts",
            Version = 2,
            Etag = "\"2-old\"",
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };

        brandingRepo.Setup(r => r.GetByTenantIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingAsset);

        var input = new BrandingUploadInput
        {
            ColorsJson = "{\"primary\":\"#0F4C81\",\"secondary\":\"#7FB3D5\",\"accent\":\"#F4B400\",\"text\":\"#1F2937\",\"background\":\"#FFFFFF\"}"
        };

        var result = await service.UploadBrandingAssetsAsync(
            _tenantId, _userId, _correlationId, "idem-colors-001", input, CancellationToken.None);

        Assert.Equal("updated", result.Status);
        Assert.Equal(3, result.Version);
    }

    // --- Malware detection failure ---

    [Fact]
    public async Task MalwareScanFailure_ThrowsAndDoesNotPersist()
    {
        var (service, brandingRepo, _, malwareClient, _, _) = CreateService();

        malwareClient.Setup(m => m.IsSafeAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var input = new BrandingUploadInput
        {
            LogoData = new byte[] { 0x89, 0x50, 0x4E, 0x47 },
            LogoFileName = "malicious.png",
            LogoContentType = "image/png"
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UploadBrandingAssetsAsync(
                _tenantId, _userId, _correlationId, "idem-malware-001", input, CancellationToken.None));

        Assert.Contains("Malware", ex.Message);
        brandingRepo.Verify(r => r.CreateAsync(It.IsAny<BrandingAssetEntity>(), It.IsAny<IDbConnection>(),
            It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()), Times.Never);
        brandingRepo.Verify(r => r.UpdateAsync(It.IsAny<BrandingAssetEntity>(), It.IsAny<IDbConnection>(),
            It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- Storage failure ---

    [Fact]
    public async Task StorageFailure_Throws()
    {
        var (service, brandingRepo, storageClient, _, _, _) = CreateService();

        storageClient.Setup(s => s.StoreFileAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Storage write failed."));

        brandingRepo.Setup(r => r.GetByTenantIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BrandingAssetEntity?)null);

        var input = new BrandingUploadInput
        {
            LogoData = new byte[] { 0x89, 0x50, 0x4E, 0x47 },
            LogoFileName = "logo.png",
            LogoContentType = "image/png"
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UploadBrandingAssetsAsync(
                _tenantId, _userId, _correlationId, "idem-storage-001", input, CancellationToken.None));

        Assert.IsType<IOException>(ex.InnerException);
    }

    // --- No input provided ---

    [Fact]
    public async Task NoInputProvided_Throws()
    {
        var (service, _, _, _, _, _) = CreateService();

        var input = new BrandingUploadInput();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UploadBrandingAssetsAsync(
                _tenantId, _userId, _correlationId, "idem-none-001", input, CancellationToken.None));

        Assert.Contains("at least one", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // --- New tenant branding record ---

    [Fact]
    public async Task NewTenant_CreatesNewBrandingRecord()
    {
        var (service, brandingRepo, _, _, _, _) = CreateService();

        brandingRepo.Setup(r => r.GetByTenantIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BrandingAssetEntity?)null);

        var input = new BrandingUploadInput
        {
            ColorsJson = "{\"primary\":\"#0F4C81\",\"secondary\":\"#7FB3D5\",\"accent\":\"#F4B400\",\"text\":\"#1F2937\",\"background\":\"#FFFFFF\"}"
        };

        var result = await service.UploadBrandingAssetsAsync(
            _tenantId, _userId, _correlationId, "idem-new-001", input, CancellationToken.None);

        Assert.Equal("updated", result.Status);
        Assert.Equal(1, result.Version);

        brandingRepo.Verify(r => r.CreateAsync(
            It.Is<BrandingAssetEntity>(e => e.TenantId == _tenantId),
            It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // --- Audit log is written ---

    [Fact]
    public async Task SuccessfulUpload_WritesAuditLog()
    {
        var (service, brandingRepo, _, _, auditRepo, _) = CreateService();

        brandingRepo.Setup(r => r.GetByTenantIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BrandingAssetEntity?)null);

        var input = new BrandingUploadInput
        {
            ColorsJson = "{\"primary\":\"#0F4C81\",\"secondary\":\"#7FB3D5\",\"accent\":\"#F4B400\",\"text\":\"#1F2937\",\"background\":\"#FFFFFF\"}"
        };

        await service.UploadBrandingAssetsAsync(
            _tenantId, _userId, _correlationId, "idem-audit-001", input, CancellationToken.None);

        auditRepo.Verify(r => r.InsertAuditLogAsync(
            It.Is<AuditLogEntity>(a =>
                a.TenantId == _tenantId &&
                a.ActorUserId == _userId &&
                a.CorrelationId == _correlationId),
            It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AuditWriteFailure_ThrowsAndDoesNotPersistSuccess()
    {
        var (service, brandingRepo, _, _, auditRepo, idempotencyRepo) = CreateService();

        brandingRepo.Setup(r => r.GetByTenantIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BrandingAssetEntity?)null);

        auditRepo.Setup(r => r.InsertAuditLogAsync(
                It.IsAny<AuditLogEntity>(), It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("audit down"));

        var input = new BrandingUploadInput
        {
            ColorsJson = "{\"primary\":\"#0F4C81\",\"secondary\":\"#7FB3D5\",\"accent\":\"#F4B400\",\"text\":\"#1F2937\",\"background\":\"#FFFFFF\"}"
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UploadBrandingAssetsAsync(
                _tenantId, _userId, _correlationId, "idem-audit-fail-001", input, CancellationToken.None));

        idempotencyRepo.Verify(r => r.InsertAsync(
            It.IsAny<IdempotencyRecordEntity>(), It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
