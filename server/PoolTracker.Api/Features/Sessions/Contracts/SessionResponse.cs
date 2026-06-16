using PoolTracker.Api.Domain.Entities;

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
    int BallsPottedOnBreak,
    int GamesWon,
    int GamesLost,
    int GamesBroken,
    int SnookersFaced,
    int SnookersEscaped,
    int GoldenBreaks,
    decimal PowerDelta,
    decimal AccuracyDelta,
    decimal CueControlDelta,
    decimal SpinDelta,
    int AwardedPoints,
    string? Notes,
    SessionEndReason? EndReason
);
