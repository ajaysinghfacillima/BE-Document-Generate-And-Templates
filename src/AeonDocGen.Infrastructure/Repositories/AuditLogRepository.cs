// TR: HLD-0004 | ORIGIN: MID_LEVEL_DESIGN-0004
using System.Data;
using AeonDocGen.Core.DTOs;
using AeonDocGen.Core.Interfaces;
using AeonDocGen.Core.Models;
using AeonDocGen.Infrastructure.Security;
using Dapper;
using Microsoft.Extensions.Options;

namespace AeonDocGen.Infrastructure.Repositories;

/// <summary>
/// Dapper-based repository for immutable audit log persistence.
/// </summary>
public sealed class AuditLogRepository : IAuditLogRepository
{
    private const string InsertSql = @"
        INSERT INTO AuditLog (
            AuditLogId, TenantId, CreatedAt, UpdatedAt, Version,
            ActorUserId, ActorType, Action, ResourceType, ResourceId,
            ScopeType, ScopeId, Outcome, IpAddress, UserAgent,
            CorrelationId, BeforeJson, AfterJson, Reason, ImmutableHash
        ) VALUES (
            @AuditLogId, @TenantId, @CreatedAt, @UpdatedAt, @Version,
            @ActorUserId, @ActorType, @Action, @ResourceType, @ResourceId,
            @ScopeType, @ScopeId, @Outcome, @IpAddress, @UserAgent,
            @CorrelationId, @BeforeJson, @AfterJson, @Reason, @ImmutableHash
        )";

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly SensitiveDataProtector _protector;

    public AuditLogRepository(IDbConnectionFactory connectionFactory, IOptions<JwtSettings> jwtSettings)
    {
        _connectionFactory = connectionFactory;
        _protector = new SensitiveDataProtector(jwtSettings);
    }

    // TR: HLD-0004 | ORIGIN: MID_LEVEL_DESIGN-0004
    public async Task InsertAuditLogAsync(AuditLogEntity auditLog, CancellationToken cancellationToken = default)
    {
        var persisted = BuildEncryptedCopy(auditLog);
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            new CommandDefinition(InsertSql, persisted, cancellationToken: cancellationToken));
    }

    // TR: HLD-0004 | ORIGIN: MID_LEVEL_DESIGN-0004
    public async Task InsertAuditLogAsync(AuditLogEntity auditLog, IDbConnection connection, IDbTransaction transaction, CancellationToken cancellationToken = default)
    {
        var persisted = BuildEncryptedCopy(auditLog);
        await connection.ExecuteAsync(
            new CommandDefinition(InsertSql, persisted, transaction: transaction, cancellationToken: cancellationToken));
    }

    private AuditLogEntity BuildEncryptedCopy(AuditLogEntity auditLog)
    {
        return new AuditLogEntity
        {
            AuditLogId = auditLog.AuditLogId,
            TenantId = auditLog.TenantId,
            CreatedAt = auditLog.CreatedAt,
            UpdatedAt = auditLog.UpdatedAt,
            Version = auditLog.Version,
            ActorUserId = auditLog.ActorUserId,
            ActorType = auditLog.ActorType,
            Action = auditLog.Action,
            ResourceType = auditLog.ResourceType,
            ResourceId = auditLog.ResourceId,
            ScopeType = auditLog.ScopeType,
            ScopeId = auditLog.ScopeId,
            Outcome = auditLog.Outcome,
            IpAddress = auditLog.IpAddress,
            UserAgent = auditLog.UserAgent,
            CorrelationId = auditLog.CorrelationId,
            BeforeJson = _protector.Encrypt(auditLog.BeforeJson),
            AfterJson = _protector.Encrypt(auditLog.AfterJson),
            Reason = _protector.Encrypt(auditLog.Reason),
            ImmutableHash = auditLog.ImmutableHash
        };
    }
}
