// TR: LLD-0021 | ORIGIN: MID_LEVEL_DESIGN-0021
using System.Data;
using AeonDocGen.Core.Models;

namespace AeonDocGen.Core.Interfaces;

/// <summary>
/// Repository contract for immutable DocumentReviewEvent persistence.
/// </summary>
public interface IDocumentReviewEventRepository
{
    Task CreateAsync(DocumentReviewEventEntity entity, IDbConnection connection, IDbTransaction transaction, CancellationToken cancellationToken = default);
}
