using AeonDocGen.Core.Interfaces;
using AeonDocGen.Core.Models;
using AeonDocGen.Core.DTOs;
using Dapper;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace AeonDocGen.Infrastructure.Repositories;

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private const string SelectSql = @"
        SELECT TokenHash AS Token, UserId, TenantId, Role, IssuedAtUtc, ExpiresAtUtc, IsRevoked
        FROM RefreshToken
        WHERE TokenHash = @TokenHash";

    private const string UpsertSql = @"
        MERGE RefreshToken AS target
        USING (SELECT @TokenHash AS TokenHash) AS source
        ON target.TokenHash = source.TokenHash
        WHEN MATCHED THEN
            UPDATE SET
                UserId = @UserId,
                TenantId = @TenantId,
                Role = @Role,
                IssuedAtUtc = @IssuedAtUtc,
                ExpiresAtUtc = @ExpiresAtUtc,
                IsRevoked = @IsRevoked
        WHEN NOT MATCHED THEN
            INSERT (TokenHash, UserId, TenantId, Role, IssuedAtUtc, ExpiresAtUtc, IsRevoked)
            VALUES (@TokenHash, @UserId, @TenantId, @Role, @IssuedAtUtc, @ExpiresAtUtc, @IsRevoked);";

    private const string RevokeSql = @"
        UPDATE RefreshToken
        SET IsRevoked = 1
        WHERE TokenHash = @TokenHash;";

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly byte[] _hashKey;

    public RefreshTokenRepository(IDbConnectionFactory connectionFactory, IOptions<JwtSettings> jwtSettings)
    {
        _connectionFactory = connectionFactory;
        _hashKey = Encoding.UTF8.GetBytes(jwtSettings.Value.SigningKey);
    }

    public Task<RefreshTokenEntity?> GetAsync(string token, CancellationToken cancellationToken = default)
    {
        var tokenHash = ComputeTokenHash(token);
        using var connection = _connectionFactory.CreateConnection();
        return connection.QuerySingleOrDefaultAsync<RefreshTokenEntity>(
            new CommandDefinition(SelectSql, new { TokenHash = tokenHash }, cancellationToken: cancellationToken));
    }

    public async Task UpsertAsync(RefreshTokenEntity entity, CancellationToken cancellationToken = default)
    {
        var tokenHash = ComputeTokenHash(entity.Token);
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(UpsertSql, new
        {
            TokenHash = tokenHash,
            entity.UserId,
            entity.TenantId,
            entity.Role,
            entity.IssuedAtUtc,
            entity.ExpiresAtUtc,
            entity.IsRevoked
        }, cancellationToken: cancellationToken));
    }

    public async Task RevokeAsync(string token, CancellationToken cancellationToken = default)
    {
        var tokenHash = ComputeTokenHash(token);
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(RevokeSql, new { TokenHash = tokenHash }, cancellationToken: cancellationToken));
    }

    private string ComputeTokenHash(string token)
    {
        using var hmac = new HMACSHA256(_hashKey);
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexStringLower(hashBytes);
    }
}
