// TR: LLD-0020 | ORIGIN: MID_LEVEL_DESIGN-0020
using System.Data;
using AeonDocGen.Core.Interfaces;
using AeonDocGen.Core.Models;
using Dapper;

namespace AeonDocGen.Infrastructure.Repositories;

/// <summary>
/// Dapper-based repository for DocumentSource linkage persistence.
/// </summary>
public sealed class DocumentSourceRepository : IDocumentSourceRepository
{
    private const string InsertSql = @"
        INSERT INTO DocumentSource (DocumentSourceId, DocumentArtifactId, SourceEntityType, SourceEntityId)
        VALUES (@DocumentSourceId, @DocumentArtifactId, @SourceEntityType, @SourceEntityId)";

    // TR: LLD-0020 | ORIGIN: MID_LEVEL_DESIGN-0020
    public async Task CreateAsync(DocumentSourceEntity entity, IDbConnection connection, IDbTransaction transaction, CancellationToken cancellationToken = default)
    {
        await connection.ExecuteAsync(
            new CommandDefinition(InsertSql, entity, transaction: transaction, cancellationToken: cancellationToken));
    }
}
