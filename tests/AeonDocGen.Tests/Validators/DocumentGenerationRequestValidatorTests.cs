// TR: LLD-0020 | ORIGIN: MID_LEVEL_DESIGN-0020
using System.Text.Json;
using AeonDocGen.Core.DTOs;
using AeonDocGen.Core.Validators;

namespace AeonDocGen.Tests.Validators;

public class DocumentGenerationRequestValidatorTests
{
    private const int MaxWatermarkLength = 200;

    private static GenerateDocumentRequestDto CreateValidRequest() => new()
    {
        DocumentType = "narrative",
        Format = "pdf",
        TemplateId = "tmpl-001",
        IncludeBranding = true,
        SourceIds = new List<string> { "src-001" }
    };

    [Fact]
    public void Validate_ValidRequest_NoErrors()
    {
        var request = CreateValidRequest();
        var errors = DocumentGenerationRequestValidator.Validate(request, MaxWatermarkLength);
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_EmptyDocumentType_ReturnsError()
    {
        var request = CreateValidRequest();
        request.DocumentType = "";
        var errors = DocumentGenerationRequestValidator.Validate(request, MaxWatermarkLength);
        Assert.Contains(errors, e => e.Contains("documentType is required"));
    }

    [Theory]
    [InlineData("narrative")]
    [InlineData("calculator")]
    [InlineData("simulationSummary")]
    [InlineData("formReadyData")]
    [InlineData("scorecard")]
    [InlineData("checklist")]
    [InlineData("report")]
    public void Validate_SupportedDocumentTypes_NoError(string docType)
    {
        var request = CreateValidRequest();
        request.DocumentType = docType;
        var errors = DocumentGenerationRequestValidator.Validate(request, MaxWatermarkLength);
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_UnsupportedDocumentType_ReturnsError()
    {
        var request = CreateValidRequest();
        request.DocumentType = "invoice";
        var errors = DocumentGenerationRequestValidator.Validate(request, MaxWatermarkLength);
        Assert.Contains(errors, e => e.Contains("documentType must be one of"));
    }

    [Fact]
    public void Validate_PackageDocumentType_ReturnsError()
    {
        var request = CreateValidRequest();
        request.DocumentType = "package";
        var errors = DocumentGenerationRequestValidator.Validate(request, MaxWatermarkLength);
        Assert.Contains(errors, e => e.Contains("documentType must be one of"));
    }

    [Fact]
    public void Validate_EmptyFormat_ReturnsError()
    {
        var request = CreateValidRequest();
        request.Format = "";
        var errors = DocumentGenerationRequestValidator.Validate(request, MaxWatermarkLength);
        Assert.Contains(errors, e => e.Contains("format is required"));
    }

    [Theory]
    [InlineData("pdf")]
    [InlineData("docx")]
    [InlineData("xlsx")]
    [InlineData("json")]
    [InlineData("pptx")]
    public void Validate_SupportedFormats_NoError(string format)
    {
        var request = CreateValidRequest();
        request.Format = format;
        var errors = DocumentGenerationRequestValidator.Validate(request, MaxWatermarkLength);
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_UnsupportedFormat_ReturnsError()
    {
        var request = CreateValidRequest();
        request.Format = "csv";
        var errors = DocumentGenerationRequestValidator.Validate(request, MaxWatermarkLength);
        Assert.Contains(errors, e => e.Contains("format must be one of"));
    }

    [Fact]
    public void Validate_EmptyTemplateId_ReturnsError()
    {
        var request = CreateValidRequest();
        request.TemplateId = "";
        var errors = DocumentGenerationRequestValidator.Validate(request, MaxWatermarkLength);
        Assert.Contains(errors, e => e.Contains("templateId is required"));
    }

    [Fact]
    public void Validate_NullSourceIds_ReturnsError()
    {
        var request = CreateValidRequest();
        request.SourceIds = null!;
        var errors = DocumentGenerationRequestValidator.Validate(request, MaxWatermarkLength);
        Assert.Contains(errors, e => e.Contains("sourceIds must contain"));
    }

    [Fact]
    public void Validate_EmptySourceIds_ReturnsError()
    {
        var request = CreateValidRequest();
        request.SourceIds = new List<string>();
        var errors = DocumentGenerationRequestValidator.Validate(request, MaxWatermarkLength);
        Assert.Contains(errors, e => e.Contains("sourceIds must contain"));
    }

    [Fact]
    public void Validate_SourceIdsWithWhitespaceEntry_ReturnsError()
    {
        var request = CreateValidRequest();
        request.SourceIds = new List<string> { "src-001", "  " };
        var errors = DocumentGenerationRequestValidator.Validate(request, MaxWatermarkLength);
        Assert.Contains(errors, e => e.Contains("must not contain empty identifiers"));
    }

    [Fact]
    public void Validate_WatermarkTextExceedsMax_ReturnsError()
    {
        var request = CreateValidRequest();
        request.WatermarkText = new string('A', 201);
        var errors = DocumentGenerationRequestValidator.Validate(request, MaxWatermarkLength);
        Assert.Contains(errors, e => e.Contains("watermarkText exceeds"));
    }

    [Fact]
    public void Validate_WatermarkTextWhitespaceOnly_ReturnsError()
    {
        var request = CreateValidRequest();
        request.WatermarkText = "   ";
        var errors = DocumentGenerationRequestValidator.Validate(request, MaxWatermarkLength);
        Assert.Contains(errors, e => e.Contains("non-empty string after trimming"));
    }

    [Fact]
    public void Validate_WatermarkTextNull_NoError()
    {
        var request = CreateValidRequest();
        request.WatermarkText = null;
        var errors = DocumentGenerationRequestValidator.Validate(request, MaxWatermarkLength);
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_WatermarkTextValid_NoError()
    {
        var request = CreateValidRequest();
        request.WatermarkText = "Draft";
        var errors = DocumentGenerationRequestValidator.Validate(request, MaxWatermarkLength);
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_MultipleErrors_ReturnsAll()
    {
        var request = new GenerateDocumentRequestDto
        {
            DocumentType = "",
            Format = "",
            TemplateId = "",
            SourceIds = new List<string>()
        };
        var errors = DocumentGenerationRequestValidator.Validate(request, MaxWatermarkLength);
        Assert.True(errors.Count >= 4);
    }

    // Project ID validation
    [Fact]
    public void ValidateProjectId_OpaqueString_ReturnsValid()
    {
        var (isValid, error) = DocumentGenerationRequestValidator.ValidateProjectId("prj-001", "trace-1");
        Assert.True(isValid);
        Assert.Null(error);
    }

    [Fact]
    public void ValidateProjectId_Empty_ReturnsError()
    {
        var (isValid, error) = DocumentGenerationRequestValidator.ValidateProjectId("", "trace-1");
        Assert.False(isValid);
        Assert.Equal("INVALID_REQUEST", error!.Code);
    }

    [Fact]
    public void ValidateProjectId_Null_ReturnsError()
    {
        var (isValid, error) = DocumentGenerationRequestValidator.ValidateProjectId(null, "trace-1");
        Assert.False(isValid);
        Assert.NotNull(error);
    }

    [Fact]
    public void ValidateProjectId_Empty_ReturnsError()
    {
        var (isValid, _) = DocumentGenerationRequestValidator.ValidateProjectId("", "trace-1");
        Assert.False(isValid);
    }

    [Fact]
    public void ValidateProjectId_Whitespace_ReturnsError()
    {
        var (isValid, error) = DocumentGenerationRequestValidator.ValidateProjectId("   ", "trace-1");
        Assert.False(isValid);
        Assert.Equal("projectId must be a non-empty identifier.", error!.Message);
    }

    [Fact]
    public void Deserialize_MissingIncludeBranding_ThrowsJsonException()
    {
        var json = """
        {
          "documentType": "narrative",
          "format": "pdf",
          "templateId": "tmpl-001",
          "sourceIds": ["src-001"]
        }
        """;

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<GenerateDocumentRequestDto>(json));
    }
}
