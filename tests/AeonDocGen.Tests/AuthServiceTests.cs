using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AeonDocGen.Core.DTOs;
using AeonDocGen.Core.Interfaces;
using AeonDocGen.Core.Models;
using AeonDocGen.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;

namespace AeonDocGen.Tests;

public class AuthServiceTests
{
    private readonly JwtSettings _settings = new()
    {
        Issuer = "test-issuer",
        Audience = "test-audience",
        SigningKey = "01234567890123456789012345678901",
        AccessTokenMinutes = 15,
        RefreshTokenDays = 30
    };

    private static string BuildToken(JwtSettings settings, Guid userId, Guid tenantId, DateTime notBefore, DateTime expires)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim("tenant_id", tenantId.ToString()),
                new Claim(ClaimTypes.Role, "Admin"),
                new Claim("permission", "documents.generate"),
                new Claim("project_id", Guid.NewGuid().ToString())
            },
            notBefore: notBefore,
            expires: expires,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public async Task ValidateTokenAsync_ValidToken_ReturnsUser()
    {
        var refreshRepo = new Mock<IRefreshTokenRepository>();
        var service = new AuthService(
            Options.Create(_settings),
            refreshRepo.Object,
            new Mock<ILogger<AuthService>>().Object);
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var token = BuildToken(_settings, userId, tenantId, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(10));

        var result = await service.ValidateTokenAsync(token, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(userId, result!.UserId);
        Assert.Equal(tenantId, result.TenantId);
        Assert.True(result.HasPermission("documents.generate"));
    }

    [Fact]
    public async Task ValidateTokenAsync_ExpiredToken_ReturnsNull()
    {
        var refreshRepo = new Mock<IRefreshTokenRepository>();
        var service = new AuthService(
            Options.Create(_settings),
            refreshRepo.Object,
            new Mock<ILogger<AuthService>>().Object);
        var token = BuildToken(_settings, Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-20), DateTime.UtcNow.AddMinutes(-10));

        var result = await service.ValidateTokenAsync(token, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateTokenAsync_NotYetValidToken_ReturnsNull()
    {
        var refreshRepo = new Mock<IRefreshTokenRepository>();
        var service = new AuthService(
            Options.Create(_settings),
            refreshRepo.Object,
            new Mock<ILogger<AuthService>>().Object);
        var token = BuildToken(_settings, Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddMinutes(5), DateTime.UtcNow.AddMinutes(30));

        var result = await service.ValidateTokenAsync(token, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task RefreshTokenAsync_RotatesAndRevokesOldToken()
    {
        var oldToken = "old-refresh-token";
        var refreshRepo = new Mock<IRefreshTokenRepository>();
        refreshRepo.Setup(r => r.GetAsync(oldToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshTokenEntity
            {
                Token = oldToken,
                UserId = Guid.NewGuid(),
                TenantId = Guid.NewGuid(),
                Role = "Admin",
                IssuedAtUtc = DateTime.UtcNow.AddDays(-1),
                ExpiresAtUtc = DateTime.UtcNow.AddDays(5),
                IsRevoked = false
            });

        var service = new AuthService(
            Options.Create(_settings),
            refreshRepo.Object,
            new Mock<ILogger<AuthService>>().Object);

        var response = await service.RefreshTokenAsync(new RefreshTokenRequestDto { RefreshToken = oldToken }, CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(response.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(response.RefreshToken));
        Assert.NotEqual(oldToken, response.RefreshToken);
        refreshRepo.Verify(r => r.RevokeAsync(oldToken, It.IsAny<CancellationToken>()), Times.Once);
        refreshRepo.Verify(r => r.UpsertAsync(It.IsAny<RefreshTokenEntity>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
