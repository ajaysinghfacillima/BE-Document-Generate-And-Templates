// TR: HLD-0004 | ORIGIN: MID_LEVEL_DESIGN-0004
using AeonDocGen.Core.DTOs;

namespace AeonDocGen.Core.Interfaces;

/// <summary>
/// Service contract for authentication token validation.
/// </summary>
public interface IAuthService
{
    Task<AuthenticatedUser?> ValidateTokenAsync(string bearerToken, CancellationToken cancellationToken = default);
    Task<RefreshTokenResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request, CancellationToken cancellationToken = default);
}
