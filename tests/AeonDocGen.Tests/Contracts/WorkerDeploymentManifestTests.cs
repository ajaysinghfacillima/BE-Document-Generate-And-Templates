namespace AeonDocGen.Tests.Contracts;

public class WorkerDeploymentManifestTests
{
    [Fact]
    public void DocumentRenderStoreWorker_ManagedProcessDeploymentManifests_AreVersionControlled()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var deploymentPath = Path.Combine(root, "src", "AeonDocGen.Api", "Deployment", "document-render-store-worker", "deployment.yaml");
        var servicePath = Path.Combine(root, "src", "AeonDocGen.Api", "Deployment", "document-render-store-worker", "aeondocgen-document-render-store-worker.service");

        Assert.True(File.Exists(deploymentPath), $"Missing deployment manifest: {deploymentPath}");
        Assert.True(File.Exists(servicePath), $"Missing managed-process service manifest: {servicePath}");

        var deployment = File.ReadAllText(deploymentPath);
        Assert.Contains("aeondocgen-document-render-store-worker", deployment);
        Assert.Contains("serviceAccountName: aeondocgen-document-render-store-worker", deployment);
        Assert.Contains("runAsNonRoot: true", deployment);
        Assert.Contains("allowPrivilegeEscalation: false", deployment);
        Assert.Contains("drop:", deployment);
        Assert.Contains("- ALL", deployment);

        var service = File.ReadAllText(servicePath);
        Assert.Contains("AEON Document Render Store Worker", service);
        Assert.Contains("ExecStart=/usr/bin/dotnet /opt/aeondocgen/AeonDocGen.Api.dll", service);
        Assert.Contains("User=aeondocgen", service);
        Assert.Contains("Group=aeondocgen", service);
        Assert.Contains("NoNewPrivileges=true", service);
        Assert.Contains("CapabilityBoundingSet=", service);
    }
}
