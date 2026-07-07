// TR: LLD-0020 | ORIGIN: MID_LEVEL_DESIGN-0020
using System.Data;
using AeonDocGen.Core.Models;

namespace AeonDocGen.Core.Interfaces;

/// <summary>
/// Repository contract for DocumentSource linkage persistence.
/// </summary>
public interface IDocumentSourceRepository
{
    Task CreateAsync(DocumentSourceEntity entity, IDbConnection connection, IDbTransaction transaction, CancellationToken cancellationToken = default);
}
