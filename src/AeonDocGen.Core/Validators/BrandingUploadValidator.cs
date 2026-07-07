// TR: LLD-0019 | ORIGIN: MID_LEVEL_DESIGN-0019
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using AeonDocGen.Core.DTOs;

namespace AeonDocGen.Core.Validators;

/// <summary>
/// Reusable validation logic for multipart branding upload payloads
/// without changing documented request schemas.
/// </summary>
public static partial class BrandingUploadValidator
{
    private static readonly HashSet<string> SupportedColorKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "primary", "secondary", "accent", "text", "background"
    };

    // TR: LLD-0019 | ORIGIN: MID_LEVEL_DESIGN-0019
    public static List<string> Validate(BrandingUploadInput input, BrandingUploadSettings settings)
    {
        var errors = new List<string>();

        if (input.LogoData == null && string.IsNullOrWhiteSpace(input.ColorsJson) && input.FontsZipData == null)
        {
            errors.Add("At least one of logoFile, colorsJson, or fontsZip must be supplied.");
            return errors;
        }

        ValidateLogoFile(input, settings, errors);
        ValidateColorsJson(input, errors);
        ValidateFontsZip(input, settings, errors);

        return errors;
    }

    // TR: LLD-0019 | ORIGIN: MID_LEVEL_DESIGN-0019
    public static void ValidateLogoFile(BrandingUploadInput input, BrandingUploadSettings settings, List<string> errors)
    {
        if (input.LogoData == null) return;

        if (input.LogoData.Length == 0)
        {
            errors.Add("logoFile size must be greater than zero.");
            return;
        }

        if (input.LogoData.Length > settings.MaxLogoSizeBytes)
        {
            errors.Add($"logoFile size exceeds the maximum allowed size of {settings.MaxLogoSizeBytes} bytes.");
        }

        if (string.IsNullOrEmpty(input.LogoContentType) ||
            !settings.AllowedLogoMimeTypes.Contains(input.LogoContentType, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add($"logoFile must be an allowed image format: {string.Join(", ", settings.AllowedLogoMimeTypes)}.");
        }
    }

    // TR: LLD-0019 | ORIGIN: MID_LEVEL_DESIGN-0019
    public static void ValidateColorsJson(BrandingUploadInput input, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(input.ColorsJson)) return;

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(input.ColorsJson);
        }
        catch (JsonException)
        {
            errors.Add("colorsJson must be valid JSON.");
            return;
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                errors.Add("colorsJson must be a JSON object.");
                return;
            }

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (!SupportedColorKeys.Contains(property.Name))
                {
                    errors.Add($"colorsJson contains unsupported key '{property.Name}'. Supported keys: {string.Join(", ", SupportedColorKeys)}.");
                    continue;
                }

                var value = property.Value.GetString();
                if (string.IsNullOrEmpty(value) || !CssHexColorRegex().IsMatch(value))
                {
                    errors.Add($"colorsJson key '{property.Name}' has an invalid CSS hex color value.");
                }
            }
        }
    }

    // TR: LLD-0019 | ORIGIN: MID_LEVEL_DESIGN-0019
    public static void ValidateFontsZip(BrandingUploadInput input, BrandingUploadSettings settings, List<string> errors)
    {
        if (input.FontsZipData == null) return;

        if (input.FontsZipData.Length == 0)
        {
            errors.Add("fontsZip size must be greater than zero.");
            return;
        }

        if (input.FontsZipData.Length > settings.MaxFontsZipSizeBytes)
        {
            errors.Add($"fontsZip size exceeds the maximum allowed size of {settings.MaxFontsZipSizeBytes} bytes.");
        }

        try
        {
            using var stream = new MemoryStream(input.FontsZipData);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

            if (archive.Entries.Count == 0)
            {
                errors.Add("fontsZip archive is empty.");
                return;
            }

            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue;
                var extension = Path.GetExtension(entry.Name);
                if (!settings.AllowedFontExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                {
                    errors.Add($"fontsZip contains unsupported file type '{extension}'. Allowed extensions: {string.Join(", ", settings.AllowedFontExtensions)}.");
                }
            }
        }
        catch (InvalidDataException)
        {
            errors.Add("fontsZip is not a valid ZIP archive.");
        }
    }

    [GeneratedRegex(@"^#([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$")]
    private static partial Regex CssHexColorRegex();
}
