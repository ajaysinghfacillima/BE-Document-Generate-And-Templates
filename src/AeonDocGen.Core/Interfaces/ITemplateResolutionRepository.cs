// TR: LLD-0020 | ORIGIN: MID_LEVEL_DESIGN-0020
using AeonDocGen.Core.Models;

namespace AeonDocGen.Core.Interfaces;

/// <summary>
/// Repository contract for template lookup and published version resolution.
/// </summary>
public interface ITemplateResolutionRepository
{
    Task<TemplateEntity?> GetByIdAsync(Guid templateId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<TemplateVersionEntity?> GetLatestPublishedVersionAsync(Guid templateId, CancellationToken cancellationToken = default);
}
