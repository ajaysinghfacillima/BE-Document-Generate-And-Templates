namespace AeonDocGen.Tests.Contracts;

public class DataAtRestEncryptionContractTests
{
    [Fact]
    public void AuditLogRepository_EncryptsSensitiveAuditPayloadFields()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var file = Path.Combine(root, "src", "AeonDocGen.Infrastructure", "Repositories", "AuditLogRepository.cs");
        Assert.True(File.Exists(file));

        var code = File.ReadAllText(file);
        Assert.Contains("_protector.Encrypt(auditLog.BeforeJson)", code);
        Assert.Contains("_protector.Encrypt(auditLog.AfterJson)", code);
        Assert.Contains("_protector.Encrypt(auditLog.Reason)", code);
    }

    [Fact]
    public void IdempotencyRepository_EncryptsStoredResponsePayload_AndDecryptsOnRead()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var file = Path.Combine(root, "src", "AeonDocGen.Infrastructure", "Repositories", "IdempotencyRepository.cs");
        Assert.True(File.Exists(file));

        var code = File.ReadAllText(file);
        Assert.Contains("_protector.Encrypt(record.ResponseJson)", code);
        Assert.Contains("_protector.Decrypt(record.ResponseJson)", code);
    }

    [Fact]
    public void DocumentReviewEventRepository_EncryptsPersistedComments()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var file = Path.Combine(root, "src", "AeonDocGen.Infrastructure", "Repositories", "DocumentReviewEventRepository.cs");
        Assert.True(File.Exists(file));

        var code = File.ReadAllText(file);
        Assert.Contains("_protector.Encrypt(entity.Comments)", code);
    }

    [Fact]
    public void SensitiveDataProtector_UsesAesGcmEnvelope()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var file = Path.Combine(root, "src", "AeonDocGen.Infrastructure", "Security", "SensitiveDataProtector.cs");
        Assert.True(File.Exists(file));

        var code = File.ReadAllText(file);
        Assert.Contains("AesGcm", code);
        Assert.Contains("enc:v1:", code);
    }
}
