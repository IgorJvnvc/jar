namespace PoolTracker.Api.Tests.Infrastructure;

public sealed record TestAuthSession(
    HttpClient Client,
    string AccessToken,
    string RefreshToken,
    Guid UserId,
    string DisplayName,
    string Email,
    string Password
);
