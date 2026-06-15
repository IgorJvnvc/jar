using PoolTracker.Api.Domain.Entities;

namespace PoolTracker.Api.Features.Auth;

public interface IJwtTokenService
{
    (string AccessToken, DateTimeOffset ExpiresAtUtc) GenerateAccessToken(ApplicationUser user);

    string GenerateRefreshToken();
}
