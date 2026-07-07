// TR: LLD-0029 | ORIGIN: MID_LEVEL_DESIGN-0029
using System.Reflection;
using System.Text.Json.Serialization;
using AeonDocGen.Core.DTOs;

namespace AeonDocGen.Tests.Contracts;

/// <summary>
/// Schema preservation tests ensuring DTO field structures match the component specification.
/// Validates StandardError, DocumentArtifact response, template list response,
/// branding asset response, and document review request/response fields.
/// </summary>
public class SchemaPreservationTests
{
    // --- StandardErrorDto schema ---

    [Fact]
    public void StandardErrorDto_HasTraceIdField()
    {
        var prop = typeof(StandardErrorDto).GetProperty("TraceId");
        Assert.NotNull(prop);
        Assert.Equal(typeof(string), prop!.PropertyType);
    }

    [Fact]
    public void StandardErrorDto_HasCodeField()
    {
        var prop = typeof(StandardErrorDto).GetProperty("Code");
        Assert.NotNull(prop);
        Assert.Equal(typeof(string), prop!.PropertyType);
    }

    [Fact]
    public void StandardErrorDto_HasMessageField()
    {
        var prop = typeof(StandardErrorDto).GetProperty("Message");
        Assert.NotNull(prop);
        Assert.Equal(typeof(string), prop!.PropertyType);
    }

    [Fact]
    public void StandardErrorDto_HasDetailsField_NullableObject()
    {
        var prop = typeof(StandardErrorDto).GetProperty("Details");
        Assert.NotNull(prop);
        Assert.Equal(typeof(object), prop!.PropertyType);
    }

    [Fact]
    public void StandardErrorDto_HasStatusField()
    {
        var prop = typeof(StandardErrorDto).GetProperty("Status");
        Assert.NotNull(prop);
        Assert.Equal(typeof(int), prop!.PropertyType);
    }

    [Fact]
    public void StandardErrorDto_HasExactlyFiveProperties()
    {
        var props = typeof(StandardErrorDto).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        Assert.Equal(5, props.Length);
    }

    // --- DocumentArtifactResponseDto schema ---

    [Theory]
    [InlineData("Id", typeof(string))]
    [InlineData("TenantId", typeof(string))]
    [InlineData("CreatedAt", typeof(DateTime))]
    [InlineData("UpdatedAt", typeof(DateTime))]
    [InlineData("Version", typeof(int))]
    [InlineData("Etag", typeof(string))]
    [InlineData("ProjectId", typeof(string))]
    [InlineData("DocumentType", typeof(string))]
    [InlineData("Format", typeof(string))]
    [InlineData("TemplateId", typeof(string))]
    [InlineData("TemplateVersion", typeof(string))]
    [InlineData("BrandingApplied", typeof(bool))]
    [InlineData("WatermarkApplied", typeof(bool))]
    [InlineData("FooterVersionText", typeof(string))]
    [InlineData("StorageUri", typeof(string))]
    [InlineData("ChecksumSha256", typeof(string))]
    [InlineData("ReviewStatus", typeof(string))]
    public void DocumentArtifactResponseDto_HasRequiredField(string fieldName, Type expectedType)
    {
        var prop = typeof(DocumentArtifactResponseDto).GetProperty(fieldName);
        Assert.NotNull(prop);
        Assert.Equal(expectedType, prop!.PropertyType);
    }

    [Fact]
    public void DocumentArtifactResponseDto_HasExactly17Properties()
    {
        var props = typeof(DocumentArtifactResponseDto).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        Assert.Equal(17, props.Length);
    }

    // --- AdminTemplateListResponseDto schema ---

    [Fact]
    public void AdminTemplateListResponseDto_HasItemsCollection()
    {
        var prop = typeof(AdminTemplateListResponseDto).GetProperty("Items");
        Assert.NotNull(prop);
        Assert.Equal(typeof(List<AdminTemplateItemDto>), prop!.PropertyType);
    }

    [Fact]
    public void AdminTemplateListResponseDto_HasOnlyItemsTopLevelField()
    {
        var props = typeof(AdminTemplateListResponseDto).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        Assert.Single(props);
        Assert.Contains(props, p => p.Name == "Items" && p.PropertyType == typeof(List<AdminTemplateItemDto>));
    }

    [Theory]
    [InlineData("Id", typeof(string))]
    [InlineData("Name", typeof(string))]
    [InlineData("CurrentVersion", typeof(string))]
    [InlineData("Versions", typeof(List<string>))]
    [InlineData("DocumentTypes", typeof(List<string>))]
    public void AdminTemplateItemDto_HasRequiredField(string fieldName, Type expectedType)
    {
        var prop = typeof(AdminTemplateItemDto).GetProperty(fieldName);
        Assert.NotNull(prop);
        Assert.Equal(expectedType, prop!.PropertyType);
    }

    [Fact]
    public void AdminTemplateItemDto_HasExactlyFiveProperties()
    {
        var props = typeof(AdminTemplateItemDto).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        Assert.Equal(5, props.Length);
    }

    // --- BrandingAssetResponseDto schema ---

    [Theory]
    [InlineData("Id", typeof(string))]
    [InlineData("TenantId", typeof(string))]
    [InlineData("CreatedAt", typeof(DateTime))]
    [InlineData("UpdatedAt", typeof(DateTime))]
    [InlineData("Version", typeof(int))]
    [InlineData("Etag", typeof(string))]
    [InlineData("Status", typeof(string))]
    public void BrandingAssetResponseDto_HasRequiredField(string fieldName, Type expectedType)
    {
        var prop = typeof(BrandingAssetResponseDto).GetProperty(fieldName);
        Assert.NotNull(prop);
        Assert.Equal(expectedType, prop!.PropertyType);
    }

    [Fact]
    public void BrandingAssetResponseDto_HasExactlySevenProperties()
    {
        var props = typeof(BrandingAssetResponseDto).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        Assert.Equal(7, props.Length);
    }

    // --- DocumentReviewRequestDto schema ---

    [Fact]
    public void DocumentReviewRequestDto_HasActionField()
    {
        var prop = typeof(DocumentReviewRequestDto).GetProperty("Action");
        Assert.NotNull(prop);
        Assert.Equal(typeof(string), prop!.PropertyType);
    }

    [Fact]
    public void DocumentReviewRequestDto_HasCommentsField_NullableString()
    {
        var prop = typeof(DocumentReviewRequestDto).GetProperty("Comments");
        Assert.NotNull(prop);
        Assert.Equal(typeof(string), prop!.PropertyType);
    }

    [Fact]
    public void DocumentReviewRequestDto_HasExactlyTwoProperties()
    {
        var props = typeof(DocumentReviewRequestDto).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        Assert.Equal(2, props.Length);
    }

    // --- DocumentReviewResponseDto schema ---

    [Theory]
    [InlineData("DocumentId", typeof(string))]
    [InlineData("ProjectId", typeof(string))]
    [InlineData("ReviewStatus", typeof(string))]
    [InlineData("Etag", typeof(string))]
    public void DocumentReviewResponseDto_HasRequiredField(string fieldName, Type expectedType)
    {
        var prop = typeof(DocumentReviewResponseDto).GetProperty(fieldName);
        Assert.NotNull(prop);
        Assert.Equal(expectedType, prop!.PropertyType);
    }

    [Fact]
    public void DocumentReviewResponseDto_HasEventField()
    {
        var prop = typeof(DocumentReviewResponseDto).GetProperty("Event");
        Assert.NotNull(prop);
        Assert.Equal(typeof(DocumentReviewEventDto), prop!.PropertyType);
    }

    [Fact]
    public void DocumentReviewResponseDto_HasOptionalReviewedByUserId()
    {
        var prop = typeof(DocumentReviewResponseDto).GetProperty("ReviewedByUserId");
        Assert.NotNull(prop);
    }

    [Fact]
    public void DocumentReviewResponseDto_HasOptionalReviewedAt()
    {
        var prop = typeof(DocumentReviewResponseDto).GetProperty("ReviewedAt");
        Assert.NotNull(prop);
    }

    // --- DocumentReviewEventDto schema ---

    [Theory]
    [InlineData("ReviewEventId")]
    [InlineData("Action")]
    [InlineData("ActorUserId")]
    [InlineData("Comments")]
    [InlineData("CreatedAt")]
    public void DocumentReviewEventDto_HasRequiredField(string fieldName)
    {
        var prop = typeof(DocumentReviewEventDto).GetProperty(fieldName);
        Assert.NotNull(prop);
    }

    // --- GenerateDocumentRequestDto schema ---

    [Theory]
    [InlineData("DocumentType", typeof(string))]
    [InlineData("Format", typeof(string))]
    [InlineData("TemplateId", typeof(string))]
    [InlineData("IncludeBranding", typeof(bool))]
    [InlineData("SourceIds", typeof(List<string>))]
    public void GenerateDocumentRequestDto_HasRequiredField(string fieldName, Type expectedType)
    {
        var prop = typeof(GenerateDocumentRequestDto).GetProperty(fieldName);
        Assert.NotNull(prop);
        Assert.Equal(expectedType, prop!.PropertyType);
    }

    [Fact]
    public void GenerateDocumentRequestDto_HasOptionalWatermarkText()
    {
        var prop = typeof(GenerateDocumentRequestDto).GetProperty("WatermarkText");
        Assert.NotNull(prop);
        Assert.Equal(typeof(string), prop!.PropertyType);
    }

    [Fact]
    public void GenerateDocumentRequestDto_IncludeBranding_IsJsonRequired()
    {
        var prop = typeof(GenerateDocumentRequestDto).GetProperty("IncludeBranding");
        Assert.NotNull(prop);
        Assert.NotNull(prop!.GetCustomAttribute<JsonRequiredAttribute>());
    }
}
