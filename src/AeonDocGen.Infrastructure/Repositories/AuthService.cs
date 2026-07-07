using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AeonDocGen.Core.DTOs;
using AeonDocGen.Core.Interfaces;
using AeonDocGen.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AeonDocGen.Infrastructure.Repositories;

public sealed class AuthService : IAuthService
{
    private readonly JwtSettings _jwtSettings;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IOptions<JwtSettings> jwtSettings,
        IRefreshTokenRepository refreshTokenRepository,
        ILogger<AuthService> logger)
    {
        _jwtSettings = jwtSettings.Value;
        ValidateSettings(_jwtSettings);

        _refreshTokenRepository = refreshTokenRepository;
        _logger = logger;
    }

    public Task<AuthenticatedUser?> ValidateTokenAsync(string bearerToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            return Task.FromResult<AuthenticatedUser?>(null);
        }

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var principal = tokenHandler.ValidateToken(bearerToken, BuildValidationParameters(), out _);

            var userIdClaim = principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value
                              ?? principal.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
            var tenantIdClaim = principal.Claims.FirstOrDefault(c => c.Type == "tenant_id")?.Value;
            var roleClaim = principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value
                            ?? principal.Claims.FirstOrDefault(c => c.Type == "role")?.Value
                            ?? string.Empty;

            if (!Guid.TryParse(userIdClaim, out var userId) || !Guid.TryParse(tenantIdClaim, out var tenantId))
            {
                return Task.FromResult<AuthenticatedUser?>(null);
            }

            var permissions = principal.Claims
                .Where(c => c.Type == "permission" || c.Type == "scope")
                .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var projectIds = principal.Claims
                .Where(c => c.Type == "project_id")
                .Select(c => Guid.TryParse(c.Value, out var pid) ? pid : Guid.Empty)
                .Where(pid => pid != Guid.Empty)
                .ToHashSet();

            return Task.FromResult<AuthenticatedUser?>(new AuthenticatedUser
            {
                UserId = userId,
                TenantId = tenantId,
                Role = roleClaim,
                Permissions = permissions,
                ProjectScopeIds = projectIds
            });
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning(ex, "Token validation failed. CorrelationId={CorrelationId}", GetCorrelationId());
            return Task.FromResult<AuthenticatedUser?>(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected token validation error. CorrelationId={CorrelationId}", GetCorrelationId());
            return Task.FromResult<AuthenticatedUser?>(null);
        }
    }

    public async Task<RefreshTokenResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            throw new UnauthorizedAccessException("Refresh token is required.");
        }

        var existing = await _refreshTokenRepository.GetAsync(request.RefreshToken, cancellationToken);
        if (existing == null || existing.IsRevoked || existing.ExpiresAtUtc <= DateTime.UtcNow)
        {
            throw new UnauthorizedAccessException("Refresh token is invalid or expired.");
        }

        var accessToken = CreateAccessToken(existing.UserId, existing.TenantId, existing.Role);
        var newRefreshToken = GenerateOpaqueToken();
        var now = DateTime.UtcNow;

        await _refreshTokenRepository.RevokeAsync(request.RefreshToken, cancellationToken);
        await _refreshTokenRepository.UpsertAsync(new RefreshTokenEntity
        {
            Token = newRefreshToken,
            UserId = existing.UserId,
            TenantId = existing.TenantId,
            Role = existing.Role,
            IssuedAtUtc = now,
            ExpiresAtUtc = now.AddDays(_jwtSettings.RefreshTokenDays),
            IsRevoked = false
        }, cancellationToken);

        return new RefreshTokenResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshToken,
            ExpiresAtUtc = now.AddMinutes(_jwtSettings.AccessTokenMinutes)
        };
    }

    private string GetCorrelationId()
    {
        return System.Diagnostics.Activity.Current?.Id ?? "n/a";
    }

    internal TokenValidationParameters BuildValidationParameters()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SigningKey));
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = _jwtSettings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            RequireExpirationTime = true,
            RequireSignedTokens = true
        };
    }

    private string CreateAccessToken(Guid userId, Guid tenantId, string role)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_jwtSettings.AccessTokenMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim("tenant_id", tenantId.ToString()),
                new Claim(ClaimTypes.Role, role),
                new Claim("permission", "documents.generate"),
                new Claim("permission", "documents.review")
            },
            notBefore: now,
            expires: expires,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateOpaqueToken()
    {
        var buffer = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(buffer);
    }

    private static void ValidateSettings(JwtSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.SigningKey))
        {
            throw new InvalidOperationException("Jwt:SigningKey must be provided via configuration or secret store.");
        }

        if (string.IsNullOrWhiteSpace(settings.Issuer))
        {
            throw new InvalidOperationException("Jwt:Issuer must be provided via configuration.");
        }

        if (string.IsNullOrWhiteSpace(settings.Audience))
        {
            throw new InvalidOperationException("Jwt:Audience must be provided via configuration.");
        }
    }
}
