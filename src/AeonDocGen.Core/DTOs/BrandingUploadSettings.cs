// TR: HLD-0019 | ORIGIN: MID_LEVEL_DESIGN-0019
namespace AeonDocGen.Core.DTOs;

/// <summary>
/// Configuration settings for branding asset upload validation and storage.
/// </summary>
public sealed class BrandingUploadSettings
{
    public string[] AllowedLogoMimeTypes { get; set; } = ["image/png", "image/jpeg", "image/svg+xml"];
    public string[] AllowedFontExtensions { get; set; } = [".ttf", ".otf", ".woff", ".woff2"];
    public long MaxLogoSizeBytes { get; set; } = 5 * 1024 * 1024;
    public long MaxFontsZipSizeBytes { get; set; } = 20 * 1024 * 1024;
    public string StorageBasePath { get; set; } = "/data/branding";
    public bool MalwareScanEnabled { get; set; } = true;
    public int MalwareScanTimeoutSeconds { get; set; } = 30;
    public string MalwareScanEndpoint { get; set; } = string.Empty;
    public int IdempotencyRetentionHours { get; set; } = 24;
}
