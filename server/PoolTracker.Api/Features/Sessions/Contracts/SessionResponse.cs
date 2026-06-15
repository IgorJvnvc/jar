namespace PoolTracker.Api.Features.Sessions.Contracts;

public sealed record SessionResponse(
    Guid Id,
    Guid PoolHallId,
    Guid? PoolHallTableId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? EndedAtUtc,
    bool IsActive,
    bool IsFlaggedForValidation,
    int BallsPotted,
    int GamesWon,
    int GamesLost,
    int SnookersEscaped,
    int AwardedPoints,
    string? Notes
);
