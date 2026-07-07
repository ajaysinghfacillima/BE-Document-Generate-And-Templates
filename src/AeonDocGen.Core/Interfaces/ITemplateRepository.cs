// TR: HLD-0019 | ORIGIN: MID_LEVEL_DESIGN-0019
using AeonDocGen.Core.Models;

namespace AeonDocGen.Core.Interfaces;

/// <summary>
/// Repository contract for tenant-scoped template and template version queries.
/// </summary>
public interface ITemplateRepository
{
    Task<IReadOnlyList<TemplateEntity>> GetTemplatesByTenantIdAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TemplateVersionEntity>> GetTemplateVersionsByTemplateIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> templateIds,
        CancellationToken cancellationToken = default);
}
