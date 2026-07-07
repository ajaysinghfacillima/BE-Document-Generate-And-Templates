// TR: LLD-0020 | ORIGIN: MID_LEVEL_DESIGN-0020
using System.Text.Json.Serialization;

namespace AeonDocGen.Core.DTOs;

/// <summary>
/// Request DTO for POST /api/v1/projects/{projectId}/documents/generate.
/// </summary>
public sealed class GenerateDocumentRequestDto
{
    public string DocumentType { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string TemplateId { get; set; } = string.Empty;
    [JsonRequired]
    public bool IncludeBranding { get; set; }
    public string? WatermarkText { get; set; }
    public List<string> SourceIds { get; set; } = new();
}
