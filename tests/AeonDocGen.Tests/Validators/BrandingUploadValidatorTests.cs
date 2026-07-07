// TR: LLD-0019 | ORIGIN: MID_LEVEL_DESIGN-0019
using AeonDocGen.Core.DTOs;
using AeonDocGen.Core.Validators;

namespace AeonDocGen.Tests.Validators;

public class BrandingUploadValidatorTests
{
    private readonly BrandingUploadSettings _settings = new()
    {
        AllowedLogoMimeTypes = ["image/png", "image/jpeg", "image/svg+xml"],
        AllowedFontExtensions = [".ttf", ".otf", ".woff", ".woff2"],
        MaxLogoSizeBytes = 5 * 1024 * 1024,
        MaxFontsZipSizeBytes = 20 * 1024 * 1024
    };

    [Fact]
    public void Validate_NoInputProvided_ReturnsError()
    {
        var input = new BrandingUploadInput();
        var errors = BrandingUploadValidator.Validate(input, _settings);
        Assert.Single(errors);
        Assert.Contains("At least one", errors[0]);
    }

    [Fact]
    public void Validate_OnlyColorsJson_Valid_NoErrors()
    {
        var input = new BrandingUploadInput { ColorsJson = "{\"primary\":\"#000000\"}" };
        var errors = BrandingUploadValidator.Validate(input, _settings);
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_OnlyLogoData_Valid_NoErrors()
    {
        var input = new BrandingUploadInput
        {
            LogoData = new byte[] { 1, 2, 3 },
            LogoFileName = "logo.png",
            LogoContentType = "image/png"
        };
        var errors = BrandingUploadValidator.Validate(input, _settings);
        Assert.Empty(errors);
    }

    // Logo validation
    [Fact]
    public void ValidateLogoFile_EmptyData_ReturnsError()
    {
        var input = new BrandingUploadInput
        {
            LogoData = Array.Empty<byte>(),
            LogoFileName = "logo.png",
            LogoContentType = "image/png"
        };
        var errors = new List<string>();
        BrandingUploadValidator.ValidateLogoFile(input, _settings, errors);
        Assert.Single(errors);
        Assert.Contains("greater than zero", errors[0]);
    }

    [Fact]
    public void ValidateLogoFile_ExceedsMaxSize_ReturnsError()
    {
        var input = new BrandingUploadInput
        {
            LogoData = new byte[6 * 1024 * 1024],
            LogoFileName = "logo.png",
            LogoContentType = "image/png"
        };
        var errors = new List<string>();
        BrandingUploadValidator.ValidateLogoFile(input, _settings, errors);
        Assert.Contains(errors, e => e.Contains("maximum allowed size"));
    }

    [Fact]
    public void ValidateLogoFile_InvalidMimeType_ReturnsError()
    {
        var input = new BrandingUploadInput
        {
            LogoData = new byte[] { 1, 2, 3 },
            LogoFileName = "logo.bmp",
            LogoContentType = "image/bmp"
        };
        var errors = new List<string>();
        BrandingUploadValidator.ValidateLogoFile(input, _settings, errors);
        Assert.Contains(errors, e => e.Contains("allowed image format"));
    }

    [Fact]
    public void ValidateLogoFile_NullContentType_ReturnsError()
    {
        var input = new BrandingUploadInput
        {
            LogoData = new byte[] { 1, 2, 3 },
            LogoFileName = "logo.png",
            LogoContentType = null
        };
        var errors = new List<string>();
        BrandingUploadValidator.ValidateLogoFile(input, _settings, errors);
        Assert.Contains(errors, e => e.Contains("allowed image format"));
    }

    [Theory]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    [InlineData("image/svg+xml")]
    public void ValidateLogoFile_AllowedMimeTypes_NoError(string mimeType)
    {
        var input = new BrandingUploadInput
        {
            LogoData = new byte[] { 1, 2, 3 },
            LogoFileName = "logo",
            LogoContentType = mimeType
        };
        var errors = new List<string>();
        BrandingUploadValidator.ValidateLogoFile(input, _settings, errors);
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateLogoFile_NullData_NoValidation()
    {
        var input = new BrandingUploadInput { LogoData = null };
        var errors = new List<string>();
        BrandingUploadValidator.ValidateLogoFile(input, _settings, errors);
        Assert.Empty(errors);
    }

    // Colors JSON validation
    [Fact]
    public void ValidateColorsJson_InvalidJson_ReturnsError()
    {
        var input = new BrandingUploadInput { ColorsJson = "not json" };
        var errors = new List<string>();
        BrandingUploadValidator.ValidateColorsJson(input, errors);
        Assert.Contains(errors, e => e.Contains("valid JSON"));
    }

    [Fact]
    public void ValidateColorsJson_Array_ReturnsError()
    {
        var input = new BrandingUploadInput { ColorsJson = "[1,2,3]" };
        var errors = new List<string>();
        BrandingUploadValidator.ValidateColorsJson(input, errors);
        Assert.Contains(errors, e => e.Contains("JSON object"));
    }

    [Fact]
    public void ValidateColorsJson_UnsupportedKey_ReturnsError()
    {
        var input = new BrandingUploadInput { ColorsJson = "{\"border\":\"#000000\"}" };
        var errors = new List<string>();
        BrandingUploadValidator.ValidateColorsJson(input, errors);
        Assert.Contains(errors, e => e.Contains("unsupported key"));
    }

    [Fact]
    public void ValidateColorsJson_InvalidHexColor_ReturnsError()
    {
        var input = new BrandingUploadInput { ColorsJson = "{\"primary\":\"red\"}" };
        var errors = new List<string>();
        BrandingUploadValidator.ValidateColorsJson(input, errors);
        Assert.Contains(errors, e => e.Contains("invalid CSS hex color"));
    }

    [Theory]
    [InlineData("#FFF")]
    [InlineData("#0F4C81")]
    [InlineData("#0F4C81FF")]
    public void ValidateColorsJson_ValidHexFormats_NoError(string hex)
    {
        var input = new BrandingUploadInput { ColorsJson = $"{{\"primary\":\"{hex}\"}}" };
        var errors = new List<string>();
        BrandingUploadValidator.ValidateColorsJson(input, errors);
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateColorsJson_AllSupportedKeys_NoError()
    {
        var input = new BrandingUploadInput
        {
            ColorsJson = "{\"primary\":\"#000000\",\"secondary\":\"#111111\",\"accent\":\"#222222\",\"text\":\"#333333\",\"background\":\"#FFFFFF\"}"
        };
        var errors = new List<string>();
        BrandingUploadValidator.ValidateColorsJson(input, errors);
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateColorsJson_NullOrWhitespace_NoValidation()
    {
        var input = new BrandingUploadInput { ColorsJson = null };
        var errors = new List<string>();
        BrandingUploadValidator.ValidateColorsJson(input, errors);
        Assert.Empty(errors);
    }

    // Fonts ZIP validation
    [Fact]
    public void ValidateFontsZip_EmptyData_ReturnsError()
    {
        var input = new BrandingUploadInput
        {
            FontsZipData = Array.Empty<byte>(),
            FontsZipFileName = "fonts.zip"
        };
        var errors = new List<string>();
        BrandingUploadValidator.ValidateFontsZip(input, _settings, errors);
        Assert.Contains(errors, e => e.Contains("greater than zero"));
    }

    [Fact]
    public void ValidateFontsZip_ExceedsMaxSize_ReturnsError()
    {
        var input = new BrandingUploadInput
        {
            FontsZipData = new byte[21 * 1024 * 1024],
            FontsZipFileName = "fonts.zip"
        };
        var errors = new List<string>();
        BrandingUploadValidator.ValidateFontsZip(input, _settings, errors);
        Assert.Contains(errors, e => e.Contains("maximum allowed size"));
    }

    [Fact]
    public void ValidateFontsZip_InvalidZip_ReturnsError()
    {
        var input = new BrandingUploadInput
        {
            FontsZipData = new byte[] { 1, 2, 3, 4, 5 },
            FontsZipFileName = "corrupt.zip"
        };
        var errors = new List<string>();
        BrandingUploadValidator.ValidateFontsZip(input, _settings, errors);
        Assert.Contains(errors, e => e.Contains("valid ZIP archive"));
    }

    [Fact]
    public void ValidateFontsZip_UnsupportedExtension_ReturnsError()
    {
        using var ms = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            var entry = archive.CreateEntry("malicious.exe");
            using var entryStream = entry.Open();
            entryStream.Write(new byte[] { 0x4D, 0x5A });
        }

        var input = new BrandingUploadInput
        {
            FontsZipData = ms.ToArray(),
            FontsZipFileName = "fonts.zip"
        };
        var errors = new List<string>();
        BrandingUploadValidator.ValidateFontsZip(input, _settings, errors);
        Assert.Contains(errors, e => e.Contains("unsupported file type"));
    }

    [Fact]
    public void ValidateFontsZip_ValidTtfFont_NoError()
    {
        using var ms = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            var entry = archive.CreateEntry("font.ttf");
            using var entryStream = entry.Open();
            entryStream.Write(new byte[] { 0, 0, 1, 0 });
        }

        var input = new BrandingUploadInput
        {
            FontsZipData = ms.ToArray(),
            FontsZipFileName = "fonts.zip"
        };
        var errors = new List<string>();
        BrandingUploadValidator.ValidateFontsZip(input, _settings, errors);
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateFontsZip_NullData_NoValidation()
    {
        var input = new BrandingUploadInput { FontsZipData = null };
        var errors = new List<string>();
        BrandingUploadValidator.ValidateFontsZip(input, _settings, errors);
        Assert.Empty(errors);
    }
}
