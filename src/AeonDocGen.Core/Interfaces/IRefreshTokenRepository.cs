using AeonDocGen.Core.Models;

namespace AeonDocGen.Core.Interfaces;

public interface IRefreshTokenRepository
{
    Task<RefreshTokenEntity?> GetAsync(string token, CancellationToken cancellationToken = default);
    Task UpsertAsync(RefreshTokenEntity entity, CancellationToken cancellationToken = default);
    Task RevokeAsync(string token, CancellationToken cancellationToken = default);
}
