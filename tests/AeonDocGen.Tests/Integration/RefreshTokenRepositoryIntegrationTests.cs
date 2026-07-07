using AeonDocGen.Core.Interfaces;
using AeonDocGen.Core.Models;
using AeonDocGen.Infrastructure.Data;
using AeonDocGen.Infrastructure.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using AeonDocGen.Core.DTOs;

namespace AeonDocGen.Tests.Integration;

public class RefreshTokenRepositoryIntegrationTests
{
    private static string? ResolveConnectionString()
    {
        var value = Environment.GetEnvironmentVariable("AEONDOCGEN_INTEGRATION_SQL_CONNECTION_STRING");
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static async Task EnsureSchemaAsync(string connectionString)
    {
        const string sql = """
            IF OBJECT_ID('RefreshToken', 'U') IS NULL
            BEGIN
                CREATE TABLE RefreshToken (
                    TokenHash NVARCHAR(128) NOT NULL PRIMARY KEY,
                    UserId UNIQUEIDENTIFIER NOT NULL,
                    TenantId UNIQUEIDENTIFIER NOT NULL,
                    Role NVARCHAR(100) NOT NULL,
                    IssuedAtUtc DATETIME2 NOT NULL,
                    ExpiresAtUtc DATETIME2 NOT NULL,
                    IsRevoked BIT NOT NULL
                );
            END;
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static IRefreshTokenRepository CreateRepository(string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString
            })
            .Build();
        IDbConnectionFactory factory = new SqlConnectionFactory(configuration);
        return new RefreshTokenRepository(factory, Options.Create(new JwtSettings
        {
            SigningKey = "01234567890123456789012345678901"
        }));
    }

    [Fact]
    public async Task RefreshTokenRepository_UpsertGetAndRevoke_UsesRealSqlBoundary()
    {
        var connectionString = ResolveConnectionString();
        if (connectionString == null)
        {
            return;
        }
        await EnsureSchemaAsync(connectionString);
        var repository = CreateRepository(connectionString);

        var token = $"it-{Guid.NewGuid():N}";
        var entity = new RefreshTokenEntity
        {
            Token = token,
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Role = "Admin",
            IssuedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(1),
            IsRevoked = false
        };

        await repository.UpsertAsync(entity);
        var inserted = await repository.GetAsync(token);
        Assert.NotNull(inserted);
        Assert.False(inserted!.IsRevoked);
        Assert.Equal(entity.UserId, inserted.UserId);

        await repository.RevokeAsync(token);
        var revoked = await repository.GetAsync(token);
        Assert.NotNull(revoked);
        Assert.True(revoked!.IsRevoked);
    }
}
