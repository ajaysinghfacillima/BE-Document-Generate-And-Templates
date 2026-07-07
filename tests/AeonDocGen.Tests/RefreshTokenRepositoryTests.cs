using AeonDocGen.Core.DTOs;
using AeonDocGen.Core.Interfaces;
using AeonDocGen.Infrastructure.Repositories;
using Microsoft.Extensions.Options;
using Moq;

namespace AeonDocGen.Tests;

public class RefreshTokenRepositoryTests
{
    [Fact]
    public void RefreshTokenHash_IsDeterministic_AndNotPlaintext()
    {
        var factory = new Mock<IDbConnectionFactory>();
        var repo = new RefreshTokenRepository(
            factory.Object,
            Options.Create(new JwtSettings
            {
                SigningKey = "01234567890123456789012345678901"
            }));

        var method = typeof(RefreshTokenRepository).GetMethod("ComputeTokenHash", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);
        var raw = "raw-refresh-token-value";

        var hash1 = (string)method!.Invoke(repo, new object[] { raw })!;
        var hash2 = (string)method.Invoke(repo, new object[] { raw })!;

        Assert.Equal(hash1, hash2);
        Assert.NotEqual(raw, hash1);
        Assert.Equal(64, hash1.Length);
    }
}
