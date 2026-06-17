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
    SessionEndReason? EndReason,
    IReadOnlyList<SessionGameDetail> Games
);

/// <summary>
/// Per-rack detail for a completed session, mirroring the raw <see cref="SessionGame"/>
/// inputs so clients can show a full rack-by-rack breakdown. Empty while a session is active
/// (racks are only persisted when the session ends).
/// </summary>
public sealed record SessionGameDetail(
    int Sequence,
    GameType GameType,
    BattleType BattleType,
    bool BrokeThisRack,
    int BreakPots,
    int BallsPotted,
    int SnookersFaced,
    int SnookersEscaped,
    bool Won,
    bool GoldenBreak,
    bool PottedTrain
);
