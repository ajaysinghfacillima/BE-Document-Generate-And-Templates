// TR: HLD-0029 | ORIGIN: MID_LEVEL_DESIGN-0029
namespace AeonDocGen.Core.DTOs;

/// <summary>
/// StandardError-compatible error response DTO.
/// </summary>
public sealed class StandardErrorDto
{
    public int Status { get; set; }
    public string TraceId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public object? Details { get; set; }
}
