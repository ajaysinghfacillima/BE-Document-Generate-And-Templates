using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AeonDocGen.Core.DTOs;

namespace AeonDocGen.Core.Services;

public static class DocumentRenderRuntime
{
    private static readonly IReadOnlyDictionary<string, string> SourceSectionTitles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["artifact"] = "Artifact Evidence",
        ["simulationJob"] = "Simulation Results",
        ["scorecard"] = "Scorecard Analysis",
        ["preAssessmentRun"] = "Pre-Assessment Findings",
        ["auditorQuery"] = "Auditor Queries",
        ["recommendation"] = "Recommendations"
    };

    public static byte[] RenderDocument(DocumentRenderStoreJobPayload payload)
    {
        if (payload.Sources == null || payload.Sources.Count == 0)
        {
            throw new InvalidOperationException("At least one source is required for document composition.");
        }

        var footerVersionText = BuildFooterVersionText(payload.FooterVersionPrefix, payload.TemplateVersion, payload.Format);
        var orderedSources = payload.Sources
            .OrderBy(s => s.EntityType, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.Id)
            .Select((s, index) => new
            {
                Position = index + 1,
                SourceType = s.EntityType,
                SourceId = s.Id,
                SectionTitle = BuildSectionTitle(s.EntityType)
            })
            .ToList();

        if (string.Equals(payload.Format, "json", StringComparison.OrdinalIgnoreCase))
        {
            var jsonDocument = new
            {
                header = new
                {
                    payload.DocumentType,
                    payload.Format,
                    payload.TemplateVersion,
                    payload.TemplateVersionId,
                    FooterVersionText = footerVersionText
                },
                templateComposition = new
                {
                    templateVersionId = payload.TemplateVersionId,
                    templateVersion = payload.TemplateVersion,
                    documentType = payload.DocumentType,
                    sourceCount = orderedSources.Count,
                    sections = orderedSources.Select(s => new
                    {
                        s.Position,
                        s.SectionTitle,
                        sourceType = s.SourceType,
                        sourceId = s.SourceId,
                        binding = $"{payload.DocumentType}:{s.SourceType}:{s.Position}"
                    })
                },
                branding = payload.BrandingApplied
                    ? new { payload.BrandingLogoStorageUri, payload.BrandingColorsJson }
                    : null,
                watermark = string.IsNullOrWhiteSpace(payload.WatermarkText) ? null : payload.WatermarkText.Trim(),
                sourceSections = orderedSources.Select(s => new
                {
                    s.Position,
                    s.SectionTitle,
                    s.SourceType,
                    s.SourceId,
                    compositionSummary = BuildCompositionSummary(s.SourceType, s.SourceId, payload.DocumentType)
                })
            };

            return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(jsonDocument));
        }

        var contentBuilder = new StringBuilder();
        contentBuilder.AppendLine($"# {payload.DocumentType.ToUpperInvariant()} DOCUMENT PACKAGE");
        contentBuilder.AppendLine($"Format: {payload.Format.ToUpperInvariant()}");
        contentBuilder.AppendLine($"TemplateVersion: {payload.TemplateVersion}");
        contentBuilder.AppendLine($"TemplateVersionId: {payload.TemplateVersionId}");
        contentBuilder.AppendLine($"SourceCount: {orderedSources.Count}");
        contentBuilder.AppendLine();
        contentBuilder.AppendLine("## Template Composition Plan");
        contentBuilder.AppendLine($"LayoutBinding: {payload.DocumentType}-template-v{payload.TemplateVersion}");
        contentBuilder.AppendLine("RenderingMode: source-composed");
        contentBuilder.AppendLine();

        foreach (var source in orderedSources)
        {
            contentBuilder.AppendLine($"## Section {source.Position}: {source.SectionTitle}");
            contentBuilder.AppendLine($"SourceType: {source.SourceType}");
            contentBuilder.AppendLine($"SourceId: {source.SourceId}");
            contentBuilder.AppendLine($"CompositionSummary: {BuildCompositionSummary(source.SourceType, source.SourceId, payload.DocumentType)}");
            contentBuilder.AppendLine();
        }

        if (payload.BrandingApplied)
        {
            contentBuilder.AppendLine($"BrandingLogo: {payload.BrandingLogoStorageUri ?? "n/a"}");
            contentBuilder.AppendLine($"BrandingColors: {payload.BrandingColorsJson ?? "{}"}");
        }

        if (!string.IsNullOrWhiteSpace(payload.WatermarkText))
        {
            contentBuilder.AppendLine($"Watermark: {payload.WatermarkText.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(footerVersionText))
        {
            contentBuilder.AppendLine($"FooterVersionText: {footerVersionText}");
        }

        return Encoding.UTF8.GetBytes(contentBuilder.ToString());
    }

    private static string BuildSectionTitle(string sourceType)
    {
        if (string.IsNullOrWhiteSpace(sourceType))
        {
            return "Source Section";
        }

        if (SourceSectionTitles.TryGetValue(sourceType, out var mappedTitle))
        {
            return mappedTitle;
        }

        return $"{char.ToUpperInvariant(sourceType[0])}{sourceType[1..]} Composition";
    }

    private static string BuildCompositionSummary(string sourceType, Guid sourceId, string documentType)
    {
        var normalizedSourceType = string.IsNullOrWhiteSpace(sourceType) ? "source" : sourceType;
        return $"Template block '{documentType}' composed with {normalizedSourceType} source '{sourceId}'.";
    }

    public static bool SupportsFooterVersion(string format)
    {
        return string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase)
            || string.Equals(format, "docx", StringComparison.OrdinalIgnoreCase)
            || string.Equals(format, "pptx", StringComparison.OrdinalIgnoreCase);
    }

    public static string BuildFooterVersionText(string footerVersionPrefix, string templateVersion, string format)
    {
        if (!SupportsFooterVersion(format))
        {
            return string.Empty;
        }

        var version = templateVersion?.Trim() ?? string.Empty;
        var prefix = footerVersionPrefix?.Trim().TrimEnd('-') ?? string.Empty;
        return string.IsNullOrWhiteSpace(prefix) ? version : $"{prefix}-{version}";
    }

    public static string ComputeChecksumSha256(byte[] content)
    {
        var hashBytes = SHA256.HashData(content);
        return Convert.ToHexStringLower(hashBytes);
    }
}
