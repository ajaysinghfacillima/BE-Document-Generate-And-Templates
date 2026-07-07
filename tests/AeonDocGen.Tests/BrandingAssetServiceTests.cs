using System.Data;
using System.Text.Json;
using AeonDocGen.Core.DTOs;
using AeonDocGen.Core.Interfaces;
using AeonDocGen.Core.Models;
using AeonDocGen.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace AeonDocGen.Tests;

public class BrandingAssetServiceTests
{
    private readonly Mock<IBrandingAssetRepository> _brandingRepoMock;
    private readonly Mock<IAuditLogRepository> _auditRepoMock;
    private readonly Mock<IIdempotencyRepository> _idempotencyRepoMock;
    private readonly Mock<IBrandingStorageClient> _storageClientMock;
    private readonly Mock<IMalwareScannerClient> _scannerMock;
    private readonly Mock<IDbConnectionFactory> _connFactoryMock;
    private readonly Mock<IDbConnection> _connectionMock;
    private readonly Mock<IDbTransaction> _transactionMock;
    private readonly BrandingAssetService _service;
    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private readonly Guid _userId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    public BrandingAssetServiceTests()
    {
        _brandingRepoMock = new Mock<IBrandingAssetRepository>();
        _auditRepoMock = new Mock<IAuditLogRepository>();
        _idempotencyRepoMock = new Mock<IIdempotencyRepository>();
        _storageClientMock = new Mock<IBrandingStorageClient>();
        _scannerMock = new Mock<IMalwareScannerClient>();
        _connFactoryMock = new Mock<IDbConnectionFactory>();
        _connectionMock = new Mock<IDbConnection>();
        _transactionMock = new Mock<IDbTransaction>();

        _connectionMock.Setup(c => c.BeginTransaction()).Returns(_transactionMock.Object);
        _connFactoryMock.Setup(f => f.CreateConnection()).Returns(_connectionMock.Object);

        _scannerMock.Setup(s => s.IsSafeAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _storageClientMock.Setup(s => s.StoreFileAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string path, byte[] _, CancellationToken _) => $"nas://{path}");

        var settings = Options.Create(new BrandingUploadSettings
        {
            AllowedLogoMimeTypes = ["image/png", "image/jpeg", "image/svg+xml"],
            AllowedFontExtensions = [".ttf", ".otf", ".woff", ".woff2"],
            MaxLogoSizeBytes = 5 * 1024 * 1024,
            MaxFontsZipSizeBytes = 20 * 1024 * 1024,
            StorageBasePath = "/data/branding",
            MalwareScanEnabled = true,
            MalwareScanTimeoutSeconds = 30,
            MalwareScanEndpoint = "http://localhost:3310/scan",
            IdempotencyRetentionHours = 24
        });

        _service = new BrandingAssetService(
            _brandingRepoMock.Object,
            _auditRepoMock.Object,
            _idempotencyRepoMock.Object,
            _storageClientMock.Object,
            _scannerMock.Object,
            _connFactoryMock.Object,
            settings,
            new Mock<ILogger<BrandingAssetService>>().Object);
    }

    [Fact]
    public async Task UploadBrandingAssets_NoInputProvided_ThrowsArgumentException()
    {
        var input = new BrandingUploadInput();
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UploadBrandingAssetsAsync(_tenantId, _userId, "corr-1", "key-1", input));
        Assert.Contains("At least one", ex.Message);
    }

    [Fact]
    public async Task UploadBrandingAssets_InvalidLogoMimeType_ThrowsArgumentException()
    {
        var input = new BrandingUploadInput
        {
            LogoData = new byte[] { 1, 2, 3 },
            LogoFileName = "logo.bmp",
            LogoContentType = "image/bmp"
        };
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UploadBrandingAssetsAsync(_tenantId, _userId, "corr-1", "key-1", input));
        Assert.Contains("allowed image format", ex.Message);
    }

    [Fact]
    public async Task UploadBrandingAssets_EmptyLogoFile_ThrowsArgumentException()
    {
        var input = new BrandingUploadInput
        {
            LogoData = Array.Empty<byte>(),
            LogoFileName = "logo.png",
            LogoContentType = "image/png"
        };
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UploadBrandingAssetsAsync(_tenantId, _userId, "corr-1", "key-1", input));
        Assert.Contains("greater than zero", ex.Message);
    }

    [Fact]
    public async Task UploadBrandingAssets_LogoExceedsMaxSize_ThrowsArgumentException()
    {
        var input = new BrandingUploadInput
        {
            LogoData = new byte[6 * 1024 * 1024],
            LogoFileName = "logo.png",
            LogoContentType = "image/png"
        };
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UploadBrandingAssetsAsync(_tenantId, _userId, "corr-1", "key-1", input));
        Assert.Contains("maximum allowed size", ex.Message);
    }

    [Fact]
    public async Task UploadBrandingAssets_InvalidColorsJson_ThrowsArgumentException()
    {
        var input = new BrandingUploadInput { ColorsJson = "not valid json" };
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UploadBrandingAssetsAsync(_tenantId, _userId, "corr-1", "key-1", input));
        Assert.Contains("valid JSON", ex.Message);
    }

    [Fact]
    public async Task UploadBrandingAssets_ColorsJsonUnsupportedKey_ThrowsArgumentException()
    {
        var input = new BrandingUploadInput { ColorsJson = "{\"unsupported\":\"#000000\"}" };
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UploadBrandingAssetsAsync(_tenantId, _userId, "corr-1", "key-1", input));
        Assert.Contains("unsupported key", ex.Message);
    }

    [Fact]
    public async Task UploadBrandingAssets_ColorsJsonInvalidHex_ThrowsArgumentException()
    {
        var input = new BrandingUploadInput { ColorsJson = "{\"primary\":\"not-a-hex\"}" };
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UploadBrandingAssetsAsync(_tenantId, _userId, "corr-1", "key-1", input));
        Assert.Contains("invalid CSS hex color", ex.Message);
    }

    [Fact]
    public async Task UploadBrandingAssets_ColorsJsonValidHex3_Succeeds()
    {
        var input = new BrandingUploadInput { ColorsJson = "{\"primary\":\"#FFF\"}" };
        _brandingRepoMock.Setup(r => r.GetByTenantIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BrandingAssetEntity?)null);

        var result = await _service.UploadBrandingAssetsAsync(_tenantId, _userId, "corr-1", "key-1", input);

        Assert.NotNull(result);
        Assert.Equal("updated", result.Status);
        Assert.Equal(1, result.Version);
    }

    [Fact]
    public async Task UploadBrandingAssets_ColorsJsonValidHex6_Succeeds()
    {
        var input = new BrandingUploadInput { ColorsJson = "{\"primary\":\"#0F4C81\",\"secondary\":\"#7FB3D5\"}" };
        _brandingRepoMock.Setup(r => r.GetByTenantIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BrandingAssetEntity?)null);

        var result = await _service.UploadBrandingAssetsAsync(_tenantId, _userId, "corr-1", "key-1", input);

        Assert.Equal("updated", result.Status);
    }

    [Fact]
    public async Task UploadBrandingAssets_MalwareScanRejectsLogo_ThrowsInvalidOperationException()
    {
        var input = new BrandingUploadInput
        {
            LogoData = new byte[] { 1, 2, 3 },
            LogoFileName = "virus.png",
            LogoContentType = "image/png"
        };
        _scannerMock.Setup(s => s.IsSafeAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UploadBrandingAssetsAsync(_tenantId, _userId, "corr-1", "key-1", input));
        Assert.Contains("Malware", ex.Message);
    }

    [Fact]
    public async Task UploadBrandingAssets_IdempotentReplay_ReturnsCachedResponse()
    {
        var cachedResponse = new BrandingAssetResponseDto
        {
            Id = "brand-001",
            TenantId = _tenantId.ToString(),
            Version = 1,
            Status = "updated"
        };
        var cachedJson = JsonSerializer.Serialize(cachedResponse);
        var input = new BrandingUploadInput { ColorsJson = "{\"primary\":\"#000000\"}" };

        _idempotencyRepoMock.Setup(r => r.GetByKeyAsync("key-1", _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdempotencyRecordEntity
            {
                IdempotencyKey = "key-1",
                TenantId = _tenantId,
                RequestHash = ComputeHashForInput(input),
                ResponseJson = cachedJson,
                StatusCode = 201
            });

        var result = await _service.UploadBrandingAssetsAsync(_tenantId, _userId, "corr-1", "key-1", input);

        Assert.Equal("brand-001", result.Id);
        Assert.Equal("updated", result.Status);
        _brandingRepoMock.Verify(r => r.CreateAsync(It.IsAny<BrandingAssetEntity>(), It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadBrandingAssets_IdempotencyKeyReusedDifferentPayload_ThrowsInvalidOperationException()
    {
        var input = new BrandingUploadInput { ColorsJson = "{\"primary\":\"#000000\"}" };

        _idempotencyRepoMock.Setup(r => r.GetByKeyAsync("key-1", _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdempotencyRecordEntity
            {
                IdempotencyKey = "key-1",
                TenantId = _tenantId,
                RequestHash = "different-hash",
                ResponseJson = "{}",
                StatusCode = 201
            });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UploadBrandingAssetsAsync(_tenantId, _userId, "corr-1", "key-1", input));
        Assert.Contains("Idempotency-Key", ex.Message);
    }

    [Fact]
    public async Task UploadBrandingAssets_NewRecord_CreatesWithVersion1()
    {
        var input = new BrandingUploadInput { ColorsJson = "{\"primary\":\"#000000\"}" };
        _brandingRepoMock.Setup(r => r.GetByTenantIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BrandingAssetEntity?)null);

        var result = await _service.UploadBrandingAssetsAsync(_tenantId, _userId, "corr-1", "key-1", input);

        Assert.Equal(1, result.Version);
        Assert.Equal("updated", result.Status);
        Assert.Equal(_tenantId.ToString(), result.TenantId);

        _brandingRepoMock.Verify(r => r.CreateAsync(It.IsAny<BrandingAssetEntity>(), It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()), Times.Once);
        _brandingRepoMock.Verify(r => r.UpdateAsync(It.IsAny<BrandingAssetEntity>(), It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadBrandingAssets_ExistingRecord_IncrementsVersion()
    {
        var existing = new BrandingAssetEntity
        {
            BrandingAssetId = Guid.NewGuid(),
            TenantId = _tenantId,
            Status = "active",
            LogoStorageUri = "nas://old-logo.png",
            ColorsJson = "{\"primary\":\"#111111\"}",
            Version = 2,
            Etag = "\"2-abc\"",
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };
        _brandingRepoMock.Setup(r => r.GetByTenantIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var input = new BrandingUploadInput { ColorsJson = "{\"primary\":\"#222222\"}" };

        var result = await _service.UploadBrandingAssetsAsync(_tenantId, _userId, "corr-1", "key-1", input);

        Assert.Equal(3, result.Version);
        Assert.Equal("updated", result.Status);

        _brandingRepoMock.Verify(r => r.UpdateAsync(It.IsAny<BrandingAssetEntity>(), It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()), Times.Once);
        _brandingRepoMock.Verify(r => r.CreateAsync(It.IsAny<BrandingAssetEntity>(), It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadBrandingAssets_PartialUpdate_PreservesOmittedFields()
    {
        var existing = new BrandingAssetEntity
        {
            BrandingAssetId = Guid.NewGuid(),
            TenantId = _tenantId,
            Status = "active",
            LogoStorageUri = "nas://existing-logo.png",
            FontsStorageUri = "nas://existing-fonts.zip",
            ColorsJson = "{\"primary\":\"#111111\"}",
            Version = 1,
            Etag = "\"1-abc\"",
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };
        _brandingRepoMock.Setup(r => r.GetByTenantIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var input = new BrandingUploadInput { ColorsJson = "{\"accent\":\"#F4B400\"}" };

        BrandingAssetEntity? capturedEntity = null;
        _brandingRepoMock.Setup(r => r.UpdateAsync(It.IsAny<BrandingAssetEntity>(), It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .Callback<BrandingAssetEntity, IDbConnection, IDbTransaction, CancellationToken>((e, _, _, _) => capturedEntity = e)
            .Returns(Task.CompletedTask);

        await _service.UploadBrandingAssetsAsync(_tenantId, _userId, "corr-1", "key-1", input);

        Assert.NotNull(capturedEntity);
        Assert.Equal("nas://existing-logo.png", capturedEntity!.LogoStorageUri);
        Assert.Equal("nas://existing-fonts.zip", capturedEntity.FontsStorageUri);
        Assert.Equal("{\"accent\":\"#F4B400\"}", capturedEntity.ColorsJson);
    }

    [Fact]
    public async Task UploadBrandingAssets_AuditLogWritten_InTransaction()
    {
        var input = new BrandingUploadInput { ColorsJson = "{\"primary\":\"#000000\"}" };
        _brandingRepoMock.Setup(r => r.GetByTenantIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BrandingAssetEntity?)null);

        await _service.UploadBrandingAssetsAsync(_tenantId, _userId, "corr-1", "key-1", input);

        _auditRepoMock.Verify(r => r.InsertAuditLogAsync(
            It.Is<AuditLogEntity>(a =>
                a.Action == "branding.assets.upload" &&
                a.ResourceType == "BrandingAsset" &&
                a.ScopeType == "tenant" &&
                a.Outcome == "success" &&
                a.CorrelationId == "corr-1"),
            It.IsAny<IDbConnection>(),
            It.IsAny<IDbTransaction>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UploadBrandingAssets_IdempotencyRecordPersisted_InTransaction()
    {
        var input = new BrandingUploadInput { ColorsJson = "{\"primary\":\"#000000\"}" };
        _brandingRepoMock.Setup(r => r.GetByTenantIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BrandingAssetEntity?)null);

        await _service.UploadBrandingAssetsAsync(_tenantId, _userId, "corr-1", "key-1", input);

        _idempotencyRepoMock.Verify(r => r.InsertAsync(
            It.Is<IdempotencyRecordEntity>(e =>
                e.IdempotencyKey == "key-1" &&
                e.TenantId == _tenantId &&
                e.StatusCode == 201),
            It.IsAny<IDbConnection>(),
            It.IsAny<IDbTransaction>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UploadBrandingAssets_WithLogoFile_StoresInTenantScopedPath()
    {
        var input = new BrandingUploadInput
        {
            LogoData = new byte[] { 0x89, 0x50, 0x4E, 0x47 },
            LogoFileName = "logo.png",
            LogoContentType = "image/png"
        };
        _brandingRepoMock.Setup(r => r.GetByTenantIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BrandingAssetEntity?)null);

        await _service.UploadBrandingAssetsAsync(_tenantId, _userId, "corr-1", "key-1", input);

        _storageClientMock.Verify(s => s.StoreFileAsync(
            It.Is<string>(p => p.Contains(_tenantId.ToString()) && p.Contains("branding/logo")),
            It.IsAny<byte[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UploadBrandingAssets_TransactionCommitted_OnSuccess()
    {
        var input = new BrandingUploadInput { ColorsJson = "{\"primary\":\"#000000\"}" };
        _brandingRepoMock.Setup(r => r.GetByTenantIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BrandingAssetEntity?)null);

        await _service.UploadBrandingAssetsAsync(_tenantId, _userId, "corr-1", "key-1", input);

        _transactionMock.Verify(t => t.Commit(), Times.Once);
        _transactionMock.Verify(t => t.Rollback(), Times.Never);
    }

    [Fact]
    public async Task UploadBrandingAssets_DbFailureAfterUpload_RollsBackTransaction()
    {
        var input = new BrandingUploadInput
        {
            LogoData = new byte[] { 1, 2, 3 },
            LogoFileName = "logo.png",
            LogoContentType = "image/png"
        };
        _brandingRepoMock.Setup(r => r.GetByTenantIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BrandingAssetEntity?)null);
        _brandingRepoMock.Setup(r => r.CreateAsync(It.IsAny<BrandingAssetEntity>(), It.IsAny<IDbConnection>(), It.IsAny<IDbTransaction>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("DB connection lost"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UploadBrandingAssetsAsync(_tenantId, _userId, "corr-1", "key-1", input));

        _transactionMock.Verify(t => t.Rollback(), Times.Once);
        _transactionMock.Verify(t => t.Commit(), Times.Never);
    }

    [Fact]
    public async Task UploadBrandingAssets_ColorsJsonNotObject_ThrowsArgumentException()
    {
        var input = new BrandingUploadInput { ColorsJson = "[\"#000000\"]" };
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UploadBrandingAssetsAsync(_tenantId, _userId, "corr-1", "key-1", input));
        Assert.Contains("JSON object", ex.Message);
    }

    [Fact]
    public async Task UploadBrandingAssets_ValidFontsZip_Succeeds()
    {
        using var ms = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            var entry = archive.CreateEntry("font.ttf");
            using var entryStream = entry.Open();
            entryStream.Write(new byte[] { 0, 0, 1, 0 });
        }

        var input = new BrandingUploadInput
        {
            FontsZipData = ms.ToArray(),
            FontsZipFileName = "fonts.zip"
        };

        _brandingRepoMock.Setup(r => r.GetByTenantIdAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BrandingAssetEntity?)null);

        var result = await _service.UploadBrandingAssetsAsync(_tenantId, _userId, "corr-1", "key-1", input);

        Assert.Equal("updated", result.Status);
    }

    [Fact]
    public async Task UploadBrandingAssets_FontsZipWithUnsupportedExtension_ThrowsArgumentException()
    {
        using var ms = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            var entry = archive.CreateEntry("malicious.exe");
            using var entryStream = entry.Open();
            entryStream.Write(new byte[] { 0x4D, 0x5A });
        }

        var input = new BrandingUploadInput
        {
            FontsZipData = ms.ToArray(),
            FontsZipFileName = "fonts.zip"
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UploadBrandingAssetsAsync(_tenantId, _userId, "corr-1", "key-1", input));
        Assert.Contains("unsupported file type", ex.Message);
    }

    [Fact]
    public async Task UploadBrandingAssets_InvalidZipArchive_ThrowsArgumentException()
    {
        var input = new BrandingUploadInput
        {
            FontsZipData = new byte[] { 1, 2, 3, 4, 5 },
            FontsZipFileName = "corrupt.zip"
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UploadBrandingAssetsAsync(_tenantId, _userId, "corr-1", "key-1", input));
        Assert.Contains("valid ZIP archive", ex.Message);
    }

    private static string ComputeHashForInput(BrandingUploadInput input)
    {
        using var sha = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        if (input.LogoData != null) sha.AppendData(input.LogoData);
        sha.AppendData(System.Text.Encoding.UTF8.GetBytes(input.ColorsJson ?? string.Empty));
        if (input.FontsZipData != null) sha.AppendData(input.FontsZipData);
        return Convert.ToHexStringLower(sha.GetHashAndReset());
    }
}
