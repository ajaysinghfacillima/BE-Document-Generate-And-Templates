using AeonDocGen.Core.DTOs;
using AeonDocGen.Infrastructure.Clients;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace AeonDocGen.Tests.Storage;

public class BrandingStorageClientTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Mock<ILogger<BrandingStorageClient>> _loggerMock;

    public BrandingStorageClientTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"branding_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _loggerMock = new Mock<ILogger<BrandingStorageClient>>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    private BrandingStorageClient CreateClient(int maxRetry = 3, int retryDelayMs = 10)
    {
        var settings = Options.Create(new StorageSettings
        {
            BrandingBasePath = _tempDir,
            DocumentBasePath = "/data/documents",
            MaxRetryAttempts = maxRetry,
            RetryBaseDelayMilliseconds = retryDelayMs
        });
        return new BrandingStorageClient(settings, _loggerMock.Object);
    }

    [Fact]
    public async Task StoreFileAsync_ValidInput_WritesFileAndReturnsNasUri()
    {
        var client = CreateClient();
        var tenantPath = "tenant-001/branding/logo/logo.png";
        var content = new byte[] { 0x89, 0x50, 0x4E, 0x47 };

        var uri = await client.StoreFileAsync(tenantPath, content);

        Assert.Equal("nas://tenant-001/branding/logo/logo.png", uri);
        var fullPath = Path.Combine(_tempDir, tenantPath);
        Assert.True(File.Exists(fullPath));
        Assert.Equal(content, await File.ReadAllBytesAsync(fullPath));
    }

    [Fact]
    public async Task StoreFileAsync_CreatesDirectoryHierarchy()
    {
        var client = CreateClient();
        var tenantPath = "tenant-002/branding/fonts/deep/nested/font.ttf";
        var content = new byte[] { 0x01, 0x02 };

        var uri = await client.StoreFileAsync(tenantPath, content);

        Assert.StartsWith("nas://", uri);
        var fullPath = Path.Combine(_tempDir, tenantPath);
        Assert.True(File.Exists(fullPath));
    }

    [Fact]
    public async Task StoreFileAsync_EmptyPath_ThrowsArgumentException()
    {
        var client = CreateClient();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.StoreFileAsync("", new byte[] { 0x01 }));
    }

    [Fact]
    public async Task StoreFileAsync_NullContent_ThrowsArgumentException()
    {
        var client = CreateClient();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.StoreFileAsync("tenant/path", null!));
    }

    [Fact]
    public async Task StoreFileAsync_EmptyContent_ThrowsArgumentException()
    {
        var client = CreateClient();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.StoreFileAsync("tenant/path", Array.Empty<byte>()));
    }

    [Fact]
    public async Task StoreFileAsync_TenantScopedPath_IsolatesByTenant()
    {
        var client = CreateClient();
        var contentA = new byte[] { 0x01 };
        var contentB = new byte[] { 0x02 };

        await client.StoreFileAsync("tenant-A/branding/logo.png", contentA);
        await client.StoreFileAsync("tenant-B/branding/logo.png", contentB);

        var fileA = await File.ReadAllBytesAsync(Path.Combine(_tempDir, "tenant-A/branding/logo.png"));
        var fileB = await File.ReadAllBytesAsync(Path.Combine(_tempDir, "tenant-B/branding/logo.png"));
        Assert.Equal(contentA, fileA);
        Assert.Equal(contentB, fileB);
    }

    [Fact]
    public async Task StoreFileAsync_OverwritesExistingFile()
    {
        var client = CreateClient();
        var path = "tenant-001/branding/logo.png";
        var original = new byte[] { 0x01 };
        var updated = new byte[] { 0x02, 0x03 };

        await client.StoreFileAsync(path, original);
        await client.StoreFileAsync(path, updated);

        var result = await File.ReadAllBytesAsync(Path.Combine(_tempDir, path));
        Assert.Equal(updated, result);
    }

    [Fact]
    public async Task DeleteFileAsync_ExistingFile_RemovesFile()
    {
        var client = CreateClient();
        var tenantPath = "tenant-001/branding/logo.png";
        await client.StoreFileAsync(tenantPath, new byte[] { 0x01 });
        var fullPath = Path.Combine(_tempDir, tenantPath);
        Assert.True(File.Exists(fullPath));

        await client.DeleteFileAsync($"nas://{tenantPath}");

        Assert.False(File.Exists(fullPath));
    }

    [Fact]
    public async Task DeleteFileAsync_NonExistentFile_DoesNotThrow()
    {
        var client = CreateClient();

        await client.DeleteFileAsync("nas://tenant-001/branding/nonexistent.png");
    }

    [Fact]
    public async Task DeleteFileAsync_EmptyUri_DoesNotThrow()
    {
        var client = CreateClient();

        await client.DeleteFileAsync("");
    }

    [Fact]
    public async Task DeleteFileAsync_NullUri_DoesNotThrow()
    {
        var client = CreateClient();

        await client.DeleteFileAsync(null!);
    }

    [Fact]
    public async Task DeleteFileAsync_UnrecognizedScheme_LogsWarningAndSkips()
    {
        var client = CreateClient();

        await client.DeleteFileAsync("s3://bucket/key");

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("unrecognized URI scheme")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task StoreFileAsync_LogsStorageOperation()
    {
        var client = CreateClient();

        await client.StoreFileAsync("tenant-001/branding/logo.png", new byte[] { 0x01 });

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Branding storage write starting")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Branding file stored successfully")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task StoreFileAsync_UsesBrandingBasePath()
    {
        var client = CreateClient();
        var tenantPath = "tenant-001/branding/logo.png";

        await client.StoreFileAsync(tenantPath, new byte[] { 0x01 });

        var expectedFullPath = Path.Combine(_tempDir, tenantPath);
        Assert.True(File.Exists(expectedFullPath));
    }

    [Fact]
    public void Constructor_BindsBrandingBasePathFromStorageSettings()
    {
        var settings = Options.Create(new StorageSettings
        {
            BrandingBasePath = "/custom/branding/path",
            DocumentBasePath = "/custom/documents/path"
        });

        var client = new BrandingStorageClient(settings, _loggerMock.Object);

        Assert.NotNull(client);
    }

    [Fact]
    public async Task StoreFileAsync_CancellationRespected()
    {
        var client = CreateClient();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.StoreFileAsync("tenant/file.bin", new byte[] { 0x01 }, cts.Token));
    }

    [Fact]
    public async Task StoreFileAsync_PathTraversalRejected()
    {
        var client = CreateClient();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.StoreFileAsync("tenant-001/../../etc/passwd", new byte[] { 0x01 }));
    }
}
