using AeonDocGen.Core.DTOs;
using AeonDocGen.Infrastructure.Clients;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace AeonDocGen.Tests.Storage;

public class FileStorageClientTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Mock<ILogger<FileStorageClient>> _loggerMock;

    public FileStorageClientTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"file_storage_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _loggerMock = new Mock<ILogger<FileStorageClient>>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    private FileStorageClient CreateClient()
    {
        var settings = Options.Create(new BrandingUploadSettings
        {
            StorageBasePath = _tempDir
        });

        return new FileStorageClient(settings, _loggerMock.Object);
    }

    [Fact]
    public async Task StoreFileAsync_RejectsTraversalPath()
    {
        var client = CreateClient();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.StoreFileAsync("tenant-001/../../etc/passwd", new byte[] { 0x01 }));
    }

    [Fact]
    public async Task StoreFileAsync_RejectsAbsolutePath()
    {
        var client = CreateClient();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.StoreFileAsync("/root/escape.txt", new byte[] { 0x01 }));
    }

    [Fact]
    public async Task DeleteFileAsync_RejectsTraversalInNasUri()
    {
        var client = CreateClient();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.DeleteFileAsync("nas://tenant-001/../secrets.txt"));
    }

    [Fact]
    public async Task StoreFileAsync_ValidRelativePath_WritesUnderBasePath()
    {
        var client = CreateClient();

        var uri = await client.StoreFileAsync("tenant-001/docs/output.bin", new byte[] { 0xAA });

        Assert.Equal("nas://tenant-001/docs/output.bin", uri);
        Assert.True(File.Exists(Path.Combine(_tempDir, "tenant-001/docs/output.bin")));
    }
}
