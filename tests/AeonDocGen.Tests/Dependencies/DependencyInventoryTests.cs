// TR: LLD-0019 | ORIGIN: MID_LEVEL_DESIGN-0019
using System.Reflection;
using System.Text.Json;
using AeonDocGen.Core.Interfaces;
using AeonDocGen.Core.Services;

namespace AeonDocGen.Tests.Dependencies;

/// <summary>
/// Dependency inventory tests verifying that object storage or NAS is listed
/// for branding upload and document generation, and that external contracts
/// remain marked as not specified by provided official documentation.
/// </summary>
public class DependencyInventoryTests
{
    // --- Branding upload depends on storage ---

    [Fact]
    public void BrandingAssetService_DependsOnStorageClient()
    {
        var ctor = typeof(BrandingAssetService).GetConstructors().First();
        var parameters = ctor.GetParameters();

        Assert.Contains(parameters, p => p.ParameterType == typeof(IBrandingStorageClient));
    }

    [Fact]
    public void BrandingAssetService_DependsOnMalwareScannerClient()
    {
        var ctor = typeof(BrandingAssetService).GetConstructors().First();
        var parameters = ctor.GetParameters();

        Assert.Contains(parameters, p => p.ParameterType == typeof(IMalwareScannerClient));
    }

    // --- Document generation depends on storage ---

    [Fact]
    public void DocumentGenerationService_DependsOnDocumentStorageClient()
    {
        var ctor = typeof(DocumentGenerationService).GetConstructors().First();
        var parameters = ctor.GetParameters();

        Assert.Contains(parameters, p => p.ParameterType == typeof(IDocumentStorageClient));
    }

    // --- Document review does NOT depend on external storage ---

    [Fact]
    public void DocumentReviewService_DoesNotDependOnStorageClient()
    {
        var ctor = typeof(DocumentReviewService).GetConstructors().First();
        var parameters = ctor.GetParameters();

        Assert.DoesNotContain(parameters, p =>
            p.ParameterType == typeof(IBrandingStorageClient) ||
            p.ParameterType == typeof(IDocumentStorageClient));
    }

    // --- Admin template service does NOT depend on external storage ---

    [Fact]
    public void AdminTemplateService_DoesNotDependOnStorageClient()
    {
        var ctor = typeof(AdminTemplateService).GetConstructors().First();
        var parameters = ctor.GetParameters();

        Assert.DoesNotContain(parameters, p =>
            p.ParameterType == typeof(IBrandingStorageClient) ||
            p.ParameterType == typeof(IDocumentStorageClient));
    }

    // --- Storage interfaces are interfaces (provider-agnostic abstractions) ---

    [Fact]
    public void IBrandingStorageClient_IsInterface()
    {
        Assert.True(typeof(IBrandingStorageClient).IsInterface);
    }

    [Fact]
    public void IDocumentStorageClient_IsInterface()
    {
        Assert.True(typeof(IDocumentStorageClient).IsInterface);
    }

    [Fact]
    public void IMalwareScannerClient_IsInterface()
    {
        Assert.True(typeof(IMalwareScannerClient).IsInterface);
    }

    // --- External contract: Object storage or NAS is unresolved ---
    // The integration catalog marks bi-object-storage-or-nas as missing_or_incomplete.
    // Verify that storage interfaces exist as abstractions but no hardcoded
    // provider-specific endpoints are present in interface declarations.

    [Fact]
    public void IBrandingStorageClient_NoHardcodedEndpoints()
    {
        var methods = typeof(IBrandingStorageClient).GetMethods();
        foreach (var method in methods)
        {
            // No method should return or accept a hardcoded provider-specific URL type
            Assert.NotEqual(typeof(Uri), method.ReturnType);
        }
    }

    [Fact]
    public void IDocumentStorageClient_NoHardcodedEndpoints()
    {
        var methods = typeof(IDocumentStorageClient).GetMethods();
        foreach (var method in methods)
        {
            Assert.NotEqual(typeof(Uri), method.ReturnType);
        }
    }

    // --- Storage is listed for branding upload and document generation APIs ---

    [Fact]
    public void BrandingAssetService_RequiresStorageDependency_ForFileOperations()
    {
        var ctor = typeof(BrandingAssetService).GetConstructors().First();
        var parameters = ctor.GetParameters();
        var storageParams = parameters.Where(p =>
            p.ParameterType == typeof(IBrandingStorageClient)).ToList();

        Assert.Single(storageParams);
    }

    [Fact]
    public void DocumentGenerationService_RequiresStorageDependency_ForFileOperations()
    {
        var ctor = typeof(DocumentGenerationService).GetConstructors().First();
        var parameters = ctor.GetParameters();
        var storageParams = parameters.Where(p =>
            p.ParameterType == typeof(IDocumentStorageClient)).ToList();

        Assert.Single(storageParams);
    }

    // --- All services have database connection factory ---

    [Theory]
    [InlineData(typeof(BrandingAssetService))]
    [InlineData(typeof(DocumentGenerationService))]
    [InlineData(typeof(DocumentReviewService))]
    public void Service_DependsOnDbConnectionFactory(Type serviceType)
    {
        var ctor = serviceType.GetConstructors().First();
        var parameters = ctor.GetParameters();

        Assert.Contains(parameters, p => p.ParameterType == typeof(IDbConnectionFactory));
    }

    // --- Idempotency repository required for POST services ---

    [Theory]
    [InlineData(typeof(BrandingAssetService))]
    [InlineData(typeof(DocumentGenerationService))]
    [InlineData(typeof(DocumentReviewService))]
    public void PostService_DependsOnIdempotencyRepository(Type serviceType)
    {
        var ctor = serviceType.GetConstructors().First();
        var parameters = ctor.GetParameters();

        Assert.Contains(parameters, p => p.ParameterType == typeof(IIdempotencyRepository));
    }

    // --- Audit log repository required for all services ---

    [Theory]
    [InlineData(typeof(AdminTemplateService))]
    [InlineData(typeof(BrandingAssetService))]
    [InlineData(typeof(DocumentGenerationService))]
    [InlineData(typeof(DocumentReviewService))]
    public void AllServices_DependOnAuditLogRepository(Type serviceType)
    {
        var ctor = serviceType.GetConstructors().First();
        var parameters = ctor.GetParameters();

        Assert.Contains(parameters, p => p.ParameterType == typeof(IAuditLogRepository));
    }

    [Fact]
    public void ExternalContracts_RemainMarkedAsNotSpecified()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var brandingStorage = File.ReadAllText(Path.Combine(root, "src", "AeonDocGen.Infrastructure", "Clients", "BrandingStorageClient.cs"));
        var documentStorage = File.ReadAllText(Path.Combine(root, "src", "AeonDocGen.Infrastructure", "Clients", "DocumentStorageClient.cs"));
        var malwareScanner = File.ReadAllText(Path.Combine(root, "src", "AeonDocGen.Infrastructure", "Clients", "MalwareScannerClient.cs"));

        Assert.Contains("Not specified by provided official documentation", brandingStorage);
        Assert.Contains("Not specified by provided official documentation", documentStorage);
        Assert.Contains("not specified by provided official documentation", malwareScanner, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IntegrationCatalog_MalwareScanningCoverage_AlignsWithRuntimeDependency()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var path = Path.Combine(root, ".codex", "spec", "integration-catalog.json");
        Assert.True(File.Exists(path));

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var backendIntegrations = doc.RootElement.GetProperty("backendIntegrations");

        var brandingCtor = typeof(BrandingAssetService).GetConstructors().First();
        var parameters = brandingCtor.GetParameters();
        Assert.Contains(parameters, p => p.ParameterType == typeof(IMalwareScannerClient));

        var malwareIntegration = backendIntegrations.EnumerateArray().FirstOrDefault(integration =>
            string.Equals(
                integration.GetProperty("integrationId").GetString(),
                "bi-malware-scanning-service",
                StringComparison.OrdinalIgnoreCase));

        Assert.Equal(JsonValueKind.Object, malwareIntegration.ValueKind);
        Assert.Equal("pp-malware-scanning-service", malwareIntegration.GetProperty("providerProfileId").GetString());
        Assert.Contains(
            malwareIntegration.GetProperty("relatedApiIds").EnumerateArray().Select(x => x.GetString()),
            id => string.Equals(id, "api-branding-assets-upload", StringComparison.Ordinal));

        var mappings = doc.RootElement.GetProperty("integrationMappings").EnumerateArray().ToList();
        var brandingMapping = mappings.First(mapping =>
            string.Equals(mapping.GetProperty("actionOrApi").GetString(), "api-branding-assets-upload", StringComparison.Ordinal));

        var mappedIntegrationIds = brandingMapping.GetProperty("integrationIds")
            .EnumerateArray()
            .Select(x => x.GetString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        Assert.Contains("bi-malware-scanning-service", mappedIntegrationIds);
    }

    [Fact]
    public void IntegrationCatalogDetail_MalwareContract_IsExplicitlyUnspecified()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var detailsRoot = Path.Combine(root, ".codex", "spec", "integration-details");
        Assert.True(Directory.Exists(detailsRoot));

        var malwareDetail = Directory.GetFiles(detailsRoot, "*.json", SearchOption.AllDirectories)
            .FirstOrDefault(file => Path.GetFileName(file).Contains("malware", StringComparison.OrdinalIgnoreCase)
                || Path.GetFileName(file).Contains("scan", StringComparison.OrdinalIgnoreCase)
                || Path.GetFileName(file).Contains("antivirus", StringComparison.OrdinalIgnoreCase));

        Assert.False(string.IsNullOrWhiteSpace(malwareDetail));

        using var doc = JsonDocument.Parse(File.ReadAllText(malwareDetail));
        var rootElement = doc.RootElement;
        Assert.Equal("bi-malware-scanning-service", rootElement.GetProperty("integrationId").GetString());
        Assert.Equal("missing_or_incomplete", rootElement.GetProperty("contractStatus").GetString());
        Assert.Contains("Not specified by provided official documentation",
            rootElement.GetProperty("warning").GetString() ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
        Assert.True(rootElement.TryGetProperty("providerContract", out var providerContract));
        Assert.Equal(JsonValueKind.Object, providerContract.ValueKind);
        Assert.Empty(providerContract.EnumerateObject());
    }
}
