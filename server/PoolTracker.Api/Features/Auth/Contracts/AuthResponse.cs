namespace PoolTracker.Api.Features.Auth.Contracts;

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    UserSummary User
);
