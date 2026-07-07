// TR: HLD-0019 | ORIGIN: MID_LEVEL_DESIGN-0019
using AeonDocGen.Core.DTOs;

namespace AeonDocGen.Core.Interfaces;

/// <summary>
/// Service contract for admin template listing operations.
/// </summary>
public interface IAdminTemplateService
{
    Task<AdminTemplateListResponseDto> ListTemplatesAsync(
        Guid tenantId,
        Guid actorUserId,
        string correlationId,
        CancellationToken cancellationToken = default);
}
