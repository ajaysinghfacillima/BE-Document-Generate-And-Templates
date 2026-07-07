using System.Security.Cryptography;
using System.Text;
using AeonDocGen.Core.DTOs;
using Microsoft.Extensions.Options;

namespace AeonDocGen.Infrastructure.Security;

internal sealed class SensitiveDataProtector
{
    private const string Prefix = "enc:v1:";
    private readonly byte[] _key;

    public SensitiveDataProtector(IOptions<JwtSettings> jwtSettings)
    {
        var signingKey = jwtSettings.Value.SigningKey;
        if (string.IsNullOrWhiteSpace(signingKey))
        {
            throw new InvalidOperationException("Sensitive data encryption requires Jwt:SigningKey to be configured.");
        }

        _key = SHA256.HashData(Encoding.UTF8.GetBytes(signingKey));
    }

    public string? Encrypt(string? plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return plainText;
        }

        var nonce = RandomNumberGenerator.GetBytes(12);
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(_key, 16);
        aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

        return $"{Prefix}{Convert.ToBase64String(nonce)}:{Convert.ToBase64String(cipherBytes)}:{Convert.ToBase64String(tag)}";
    }

    public string? Decrypt(string? cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
        {
            return cipherText;
        }

        if (!cipherText.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return cipherText;
        }

        var payload = cipherText[Prefix.Length..];
        var parts = payload.Split(':');
        if (parts.Length != 3)
        {
            throw new InvalidOperationException("Encrypted payload format is invalid.");
        }

        var nonce = Convert.FromBase64String(parts[0]);
        var cipherBytes = Convert.FromBase64String(parts[1]);
        var tag = Convert.FromBase64String(parts[2]);
        var plainBytes = new byte[cipherBytes.Length];

        using var aes = new AesGcm(_key, 16);
        aes.Decrypt(nonce, cipherBytes, tag, plainBytes);
        return Encoding.UTF8.GetString(plainBytes);
    }
}
