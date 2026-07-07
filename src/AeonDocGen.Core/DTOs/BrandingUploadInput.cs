// TR: HLD-0019 | ORIGIN: MID_LEVEL_DESIGN-0019
namespace AeonDocGen.Core.DTOs;

/// <summary>
/// Input model for branding asset upload, decoupled from ASP.NET Core IFormFile.
/// </summary>
public sealed class BrandingUploadInput
{
    public byte[]? LogoData { get; set; }
    public string? LogoFileName { get; set; }
    public string? LogoContentType { get; set; }
    public string? ColorsJson { get; set; }
    public byte[]? FontsZipData { get; set; }
    public string? FontsZipFileName { get; set; }
}
