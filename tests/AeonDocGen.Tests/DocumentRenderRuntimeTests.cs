using System.Text;
using AeonDocGen.Core.DTOs;
using AeonDocGen.Core.Services;

namespace AeonDocGen.Tests;

public class DocumentRenderRuntimeTests
{
    [Fact]
    public void BuildFooterVersionText_SupportedFormat_ReturnsExpectedFooter()
    {
        var footer = DocumentRenderRuntime.BuildFooterVersionText("v1.0", "2.3", "pdf");
        Assert.Equal("v1.0-2.3", footer);
    }

    [Fact]
    public void BuildFooterVersionText_UnsupportedFormat_ReturnsEmpty()
    {
        var footer = DocumentRenderRuntime.BuildFooterVersionText("v1.0", "2.3", "xlsx");
        Assert.Equal(string.Empty, footer);
    }

    [Fact]
    public void BuildFooterVersionText_EmptyPrefix_FallsBackToTemplateVersion()
    {
        var footer = DocumentRenderRuntime.BuildFooterVersionText(string.Empty, "2.3", "docx");
        Assert.Equal("2.3", footer);
    }

    [Fact]
    public void RenderDocument_JsonFormat_ContainsFooterAndSourceSections()
    {
        var payload = CreatePayload("json");

        var bytes = DocumentRenderRuntime.RenderDocument(payload);
        var rendered = Encoding.UTF8.GetString(bytes);

        Assert.Contains("\"FooterVersionText\":\"\"", rendered);
        Assert.Contains("\"sourceSections\"", rendered);
        Assert.Contains(payload.Sources[0].Id.ToString(), rendered);
        Assert.Contains("\"templateComposition\"", rendered);
        Assert.Contains("\"compositionSummary\"", rendered);
        Assert.DoesNotContain("generatedAtUtc", rendered);
    }

    [Fact]
    public void RenderDocument_PdfFormat_ContainsFooterVersionText()
    {
        var payload = CreatePayload("pdf");

        var bytes = DocumentRenderRuntime.RenderDocument(payload);
        var rendered = Encoding.UTF8.GetString(bytes);

        Assert.Contains("FooterVersionText: v2-1.4", rendered);
        Assert.Contains("Template Composition Plan", rendered);
        Assert.Contains("Section 1:", rendered);
        Assert.DoesNotContain("Resolved content for", rendered);
    }

    [Fact]
    public void RenderDocument_NoSources_ThrowsInvalidOperationException()
    {
        var payload = CreatePayload("pdf");
        payload.Sources = new List<DocumentRenderSourcePayload>();

        Assert.Throws<InvalidOperationException>(() => DocumentRenderRuntime.RenderDocument(payload));
    }

    private static DocumentRenderStoreJobPayload CreatePayload(string format)
    {
        return new DocumentRenderStoreJobPayload
        {
            TenantId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            DocumentId = Guid.NewGuid(),
            CorrelationId = "corr-render",
            FooterVersionPrefix = "v2",
            DocumentType = "narrative",
            Format = format,
            WatermarkText = "Draft",
            BrandingApplied = true,
            TemplateVersion = "1.4",
            TemplateVersionId = Guid.NewGuid(),
            BrandingLogoStorageUri = "nas://logo",
            BrandingColorsJson = "{\"primary\":\"#112233\"}",
            Sources = new List<DocumentRenderSourcePayload>
            {
                new() { Id = Guid.Parse("00000000-0000-0000-0000-0000000000AA"), EntityType = "artifact" },
                new() { Id = Guid.Parse("00000000-0000-0000-0000-0000000000BB"), EntityType = "scorecard" }
            }
        };
    }
}
