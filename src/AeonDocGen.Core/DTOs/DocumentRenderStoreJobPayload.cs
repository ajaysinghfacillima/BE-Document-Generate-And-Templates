namespace AeonDocGen.Core.DTOs;

public sealed class DocumentRenderStoreJobPayload
{
    public Guid TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid DocumentId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string FooterVersionPrefix { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string? WatermarkText { get; set; }
    public bool BrandingApplied { get; set; }
    public string TemplateVersion { get; set; } = string.Empty;
    public Guid TemplateVersionId { get; set; }
    public string? BrandingLogoStorageUri { get; set; }
    public string? BrandingColorsJson { get; set; }
    public List<DocumentRenderSourcePayload> Sources { get; set; } = new();
}

public sealed class DocumentRenderSourcePayload
{
    public Guid Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
}

public sealed class DocumentRenderStoreJobResult
{
    public string StorageUri { get; set; } = string.Empty;
    public string ChecksumSha256 { get; set; } = string.Empty;
    public bool WatermarkApplied { get; set; }
    public string FooterVersionText { get; set; } = string.Empty;
}
