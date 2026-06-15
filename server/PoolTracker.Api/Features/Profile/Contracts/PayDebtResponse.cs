namespace PoolTracker.Api.Features.Profile.Contracts;

public sealed record PayDebtResponse(
    int PaidPoints,
    ProfileResponse Profile
);
