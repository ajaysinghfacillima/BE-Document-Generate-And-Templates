using System.Security.Cryptography;
using System.Text;

namespace AeonDocGen.Core.Utilities;

/// <summary>
/// Normalizes public identifier strings into stable Guid values.
/// Preserves existing Guid inputs and deterministically maps opaque strings.
/// </summary>
public static class OpaqueIdentifier
{
    public static bool TryNormalize(string? value, string scope, out Guid identifier)
    {
        identifier = Guid.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (Guid.TryParse(value, out identifier) && identifier != Guid.Empty)
        {
            return true;
        }

        var payload = $"{scope}:{value.Trim()}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, bytes.Length).CopyTo(bytes);
        identifier = new Guid(bytes);
        return true;
    }
}
