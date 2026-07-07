// TR: LLD-0021 | ORIGIN: MID_LEVEL_DESIGN-0021
using System.Data;
using AeonDocGen.Core.DTOs;
using AeonDocGen.Core.Interfaces;
using AeonDocGen.Core.Models;
using AeonDocGen.Infrastructure.Security;
using Dapper;
using Microsoft.Extensions.Options;

namespace AeonDocGen.Infrastructure.Repositories;

/// <summary>
/// Dapper-based repository for immutable DocumentReviewEvent persistence.
/// </summary>
public sealed class DocumentReviewEventRepository : IDocumentReviewEventRepository
{
    private const string InsertSql = @"
        INSERT INTO DocumentReviewEvent (DocumentReviewEventId, DocumentArtifactId, Action, ActorUserId, Comments, CreatedAt)
        VALUES (@DocumentReviewEventId, @DocumentArtifactId, @Action, @ActorUserId, @Comments, @CreatedAt)";

    private readonly SensitiveDataProtector _protector;

    public DocumentReviewEventRepository(IOptions<JwtSettings> jwtSettings)
    {
        _protector = new SensitiveDataProtector(jwtSettings);
    }

    // TR: LLD-0021 | ORIGIN: MID_LEVEL_DESIGN-0021
    public async Task CreateAsync(DocumentReviewEventEntity entity, IDbConnection connection, IDbTransaction transaction, CancellationToken cancellationToken = default)
    {
        var persisted = new DocumentReviewEventEntity
        {
            DocumentReviewEventId = entity.DocumentReviewEventId,
            DocumentArtifactId = entity.DocumentArtifactId,
            Action = entity.Action,
            ActorUserId = entity.ActorUserId,
            Comments = _protector.Encrypt(entity.Comments),
            CreatedAt = entity.CreatedAt
        };

        await connection.ExecuteAsync(
            new CommandDefinition(InsertSql, persisted, transaction: transaction, cancellationToken: cancellationToken));
    }
}
