using AeonDocGen.Core.DTOs;
using AeonDocGen.Infrastructure.Clients;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace AeonDocGen.Tests.Storage;

public class DocumentStorageClientTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Mock<ILogger<DocumentStorageClient>> _loggerMock;

    public DocumentStorageClientTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"document_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _loggerMock = new Mock<ILogger<DocumentStorageClient>>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    private DocumentStorageClient CreateClient(int maxRetry = 3, int retryDelayMs = 10)
    {
        var settings = Options.Create(new StorageSettings
        {
            BrandingBasePath = "/data/branding",
            DocumentBasePath = _tempDir,
            MaxRetryAttempts = maxRetry,
            RetryBaseDelayMilliseconds = retryDelayMs
        });
        return new DocumentStorageClient(settings, _loggerMock.Object);
    }

    [Fact]
    public async Task StoreFileAsync_ValidInput_WritesFileAndReturnsNasUri()
    {
        var client = CreateClient();
        var tenantPath = "tenant-001/projects/prj-001/generated/doc-001/narrative.pdf";
        var content = new byte[] { 0x25, 0x50, 0x44, 0x46 };

        var uri = await client.StoreFileAsync(tenantPath, content);

        Assert.Equal($"nas://{tenantPath}", uri);
        var fullPath = Path.Combine(_tempDir, tenantPath);
        Assert.True(File.Exists(fullPath));
        Assert.Equal(content, await File.ReadAllBytesAsync(fullPath));
    }

    [Fact]
    public async Task StoreFileAsync_CreatesDirectoryHierarchy()
    {
        var client = CreateClient();
        var tenantPath = "tenant-002/projects/prj-002/generated/doc-002/report.docx";
        var content = new byte[] { 0x01, 0x02 };

        var uri = await client.StoreFileAsync(tenantPath, content);

        Assert.StartsWith("nas://", uri);
        Assert.True(File.Exists(Path.Combine(_tempDir, tenantPath)));
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

        await client.StoreFileAsync("tenant-A/projects/p1/generated/doc/out.pdf", contentA);
        await client.StoreFileAsync("tenant-B/projects/p1/generated/doc/out.pdf", contentB);

        var fileA = await File.ReadAllBytesAsync(Path.Combine(_tempDir, "tenant-A/projects/p1/generated/doc/out.pdf"));
        var fileB = await File.ReadAllBytesAsync(Path.Combine(_tempDir, "tenant-B/projects/p1/generated/doc/out.pdf"));
        Assert.Equal(contentA, fileA);
        Assert.Equal(contentB, fileB);
    }

    [Fact]
    public async Task StoreFileAsync_OverwritesExistingFile()
    {
        var client = CreateClient();
        var path = "tenant-001/projects/p1/generated/doc/out.pdf";
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
        var tenantPath = "tenant-001/projects/p1/generated/doc/out.pdf";
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

        await client.DeleteFileAsync("nas://tenant-001/projects/p1/generated/doc/nonexistent.pdf");
    }

    [Fact]
    public async Task DeleteFileAsync_EmptyUri_DoesNotThrow()
    {
        var client = CreateClient();

        await client.DeleteFileAsync("");
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
    public async Task StoreFileAsync_LogsDocumentStorageOperation()
    {
        var client = CreateClient();

        await client.StoreFileAsync("tenant-001/projects/p1/generated/doc/out.pdf", new byte[] { 0x01 });

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Document storage write starting")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Document file stored successfully")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task StoreFileAsync_UsesDocumentBasePath()
    {
        var client = CreateClient();
        var tenantPath = "tenant-001/projects/p1/generated/doc/out.pdf";

        await client.StoreFileAsync(tenantPath, new byte[] { 0x01 });

        var expectedFullPath = Path.Combine(_tempDir, tenantPath);
        Assert.True(File.Exists(expectedFullPath));
    }

    [Fact]
    public void Constructor_BindsDocumentBasePathFromStorageSettings()
    {
        var settings = Options.Create(new StorageSettings
        {
            BrandingBasePath = "/custom/branding/path",
            DocumentBasePath = "/custom/documents/path"
        });

        var client = new DocumentStorageClient(settings, _loggerMock.Object);

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
    public async Task StoreFileAsync_WhitespacePath_ThrowsArgumentException()
    {
        var client = CreateClient();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.StoreFileAsync("   ", new byte[] { 0x01 }));
    }

    [Fact]
    public async Task DeleteFileAsync_NullUri_DoesNotThrow()
    {
        var client = CreateClient();

        await client.DeleteFileAsync(null!);
    }

    [Fact]
    public async Task StoreFileAsync_PathTraversalRejected()
    {
        var client = CreateClient();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.StoreFileAsync("tenant-001/projects/../../secrets.txt", new byte[] { 0x01 }));
    }
}
