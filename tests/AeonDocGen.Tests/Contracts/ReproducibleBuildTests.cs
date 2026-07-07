using System.Text.Json;

namespace AeonDocGen.Tests.Contracts;

public class ReproducibleBuildTests
{
    [Fact]
    public void GlobalJson_Exists_WithSdkPinned()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var globalJsonPath = Path.Combine(root, "global.json");
        Assert.True(File.Exists(globalJsonPath));

        using var doc = JsonDocument.Parse(File.ReadAllText(globalJsonPath));
        var sdk = doc.RootElement.GetProperty("sdk");
        var version = sdk.GetProperty("version").GetString();
        Assert.False(string.IsNullOrWhiteSpace(version));
    }

    [Fact]
    public void PackagesLockFiles_AreCommitted_ForAllProjects()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var projectPaths = new[]
        {
            Path.Combine(root, "src", "AeonDocGen.Api", "packages.lock.json"),
            Path.Combine(root, "src", "AeonDocGen.Core", "packages.lock.json"),
            Path.Combine(root, "src", "AeonDocGen.Infrastructure", "packages.lock.json"),
            Path.Combine(root, "tests", "AeonDocGen.Tests", "packages.lock.json")
        };

        Assert.All(projectPaths, path => Assert.True(File.Exists(path), $"Missing lock file: {path}"));
    }
}
