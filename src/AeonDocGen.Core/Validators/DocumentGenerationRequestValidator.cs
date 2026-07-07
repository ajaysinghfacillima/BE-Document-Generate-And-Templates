// TR: LLD-0020 | ORIGIN: MID_LEVEL_DESIGN-0020
using AeonDocGen.Core.DTOs;
using AeonDocGen.Core.Utilities;

namespace AeonDocGen.Core.Validators;

/// <summary>
/// Reusable validation logic for document generation request payloads
/// without changing documented request schemas.
/// </summary>
public static class DocumentGenerationRequestValidator
{
    private static readonly HashSet<string> SupportedDocumentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "narrative", "calculator", "simulationSummary", "formReadyData", "scorecard", "checklist", "report"
    };

    private static readonly HashSet<string> SupportedFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "pdf", "docx", "xlsx", "json", "pptx"
    };

    // TR: LLD-0020 | ORIGIN: MID_LEVEL_DESIGN-0020
    public static List<string> Validate(GenerateDocumentRequestDto request, int maxWatermarkLength)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.DocumentType))
        {
            errors.Add("documentType is required.");
        }
        else if (!SupportedDocumentTypes.Contains(request.DocumentType))
        {
            errors.Add($"documentType must be one of: {string.Join(", ", SupportedDocumentTypes)}.");
        }

        if (string.IsNullOrWhiteSpace(request.Format))
        {
            errors.Add("format is required.");
        }
        else if (!SupportedFormats.Contains(request.Format))
        {
            errors.Add($"format must be one of: {string.Join(", ", SupportedFormats)}.");
        }

        if (string.IsNullOrWhiteSpace(request.TemplateId))
        {
            errors.Add("templateId is required.");
        }

        if (request.SourceIds == null || request.SourceIds.Count == 0)
        {
            errors.Add("sourceIds must contain at least one source identifier.");
        }
        else if (request.SourceIds.Any(string.IsNullOrWhiteSpace))
        {
            errors.Add("sourceIds must not contain empty identifiers.");
        }

        if (!string.IsNullOrEmpty(request.WatermarkText))
        {
            var trimmed = request.WatermarkText.Trim();
            if (trimmed.Length == 0)
            {
                errors.Add("watermarkText, if provided, must be a non-empty string after trimming.");
            }
            else if (trimmed.Length > maxWatermarkLength)
            {
                errors.Add($"watermarkText exceeds the maximum allowed length of {maxWatermarkLength} characters.");
            }
        }

        return errors;
    }

    // TR: LLD-0020 | ORIGIN: MID_LEVEL_DESIGN-0020
    public static (bool IsValid, StandardErrorDto? Error) ValidateProjectId(string? projectId, string traceId)
    {
        if (!OpaqueIdentifier.TryNormalize(projectId, "project", out _))
        {
            return (false, new StandardErrorDto
            {
                TraceId = traceId,
                Code = "INVALID_REQUEST",
                Message = "projectId must be a non-empty identifier."
            });
        }
        return (true, null);
    }
}
