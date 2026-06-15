namespace PoolTracker.Api.Features.Auth.Contracts;

public sealed record UserSummary(
    Guid Id,
    string DisplayName,
    string Email
);
