// TR: HLD-0019 | ORIGIN: MID_LEVEL_DESIGN-0019
namespace AeonDocGen.Core.DTOs;

/// <summary>
/// Response DTO for GET /api/v1/admin/templates.
/// </summary>
public sealed class AdminTemplateListResponseDto
{
    public List<AdminTemplateItemDto> Items { get; set; } = new();
}

/// <summary>
/// A single template item in the admin template list response.
/// </summary>
public sealed class AdminTemplateItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CurrentVersion { get; set; } = string.Empty;
    public List<string> Versions { get; set; } = new();
    public List<string> DocumentTypes { get; set; } = new();
}
