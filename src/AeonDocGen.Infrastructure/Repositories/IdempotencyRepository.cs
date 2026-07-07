// TR: HLD-0029 | ORIGIN: MID_LEVEL_DESIGN-0029
using System.Data;
using AeonDocGen.Core.DTOs;
using AeonDocGen.Core.Interfaces;
using AeonDocGen.Core.Models;
using AeonDocGen.Infrastructure.Security;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AeonDocGen.Infrastructure.Repositories;

/// <summary>
/// Dapper-based repository for idempotent request replay tracking.
/// Ensures the IdempotencyRecord table exists on first access.
/// </summary>
public sealed class IdempotencyRepository : IIdempotencyRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<IdempotencyRepository> _logger;
    private readonly SensitiveDataProtector _protector;
    private volatile bool _tableEnsured;

    public IdempotencyRepository(
        IDbConnectionFactory connectionFactory,
        ILogger<IdempotencyRepository> logger,
        IOptions<JwtSettings> jwtSettings)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
        _protector = new SensitiveDataProtector(jwtSettings);
    }

    // TR: HLD-0029 | ORIGIN: MID_LEVEL_DESIGN-0029
    public async Task<IdempotencyRecordEntity?> GetByKeyAsync(string idempotencyKey, Guid tenantId, CancellationToken cancellationToken = default)
    {
        await EnsureTableExistsAsync(cancellationToken);

        const string sql = @"
            SELECT IdempotencyKey, TenantId, RequestHash, ResponseJson, StatusCode, CreatedAt
            FROM IdempotencyRecord
            WHERE IdempotencyKey = @IdempotencyKey AND TenantId = @TenantId";

        using var connection = _connectionFactory.CreateConnection();
        var record = await connection.QuerySingleOrDefaultAsync<IdempotencyRecordEntity>(
            new CommandDefinition(sql, new { IdempotencyKey = idempotencyKey, TenantId = tenantId }, cancellationToken: cancellationToken));
        if (record == null)
        {
            return null;
        }

        record.ResponseJson = _protector.Decrypt(record.ResponseJson) ?? string.Empty;
        return record;
    }

    // TR: HLD-0029 | ORIGIN: MID_LEVEL_DESIGN-0029
    public async Task InsertAsync(IdempotencyRecordEntity record, IDbConnection connection, IDbTransaction transaction, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            INSERT INTO IdempotencyRecord (IdempotencyKey, TenantId, RequestHash, ResponseJson, StatusCode, CreatedAt)
            VALUES (@IdempotencyKey, @TenantId, @RequestHash, @ResponseJson, @StatusCode, @CreatedAt)";

        var persisted = new IdempotencyRecordEntity
        {
            IdempotencyKey = record.IdempotencyKey,
            TenantId = record.TenantId,
            RequestHash = record.RequestHash,
            ResponseJson = _protector.Encrypt(record.ResponseJson) ?? string.Empty,
            StatusCode = record.StatusCode,
            CreatedAt = record.CreatedAt
        };

        await connection.ExecuteAsync(
            new CommandDefinition(sql, persisted, transaction: transaction, cancellationToken: cancellationToken));
    }

    private async Task EnsureTableExistsAsync(CancellationToken cancellationToken)
    {
        if (_tableEnsured) return;

        const string sql = @"
            IF OBJECT_ID('IdempotencyRecord', 'U') IS NULL
            CREATE TABLE IdempotencyRecord (
                IdempotencyKey NVARCHAR(256) NOT NULL,
                TenantId UNIQUEIDENTIFIER NOT NULL,
                RequestHash NVARCHAR(128) NOT NULL,
                ResponseJson NVARCHAR(MAX) NOT NULL,
                StatusCode INT NOT NULL,
                CreatedAt DATETIME2(3) NOT NULL,
                CONSTRAINT PK_IdempotencyRecord PRIMARY KEY CLUSTERED (IdempotencyKey, TenantId)
            )";

        try
        {
            using var connection = _connectionFactory.CreateConnection();
            await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));
            _tableEnsured = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Could not ensure IdempotencyRecord table exists. Table may already exist or creation requires elevated permissions. RequestId={RequestId}",
                System.Diagnostics.Activity.Current?.Id ?? "n/a");
        }
    }
}
