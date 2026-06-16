namespace PoolTracker.Api.Features.Halls.Contracts;

public sealed record HallDayCompetitionEntryResponse(
    Guid UserId,
    string DisplayName,
    int Rank,
    int GamesWon,
    int GamesLost,
    int BallsPotted,
    int SessionsCompleted,
    int MinutesPlayed
);

public sealed record HallDayCompetitionResponse(
    Guid PoolHallId,
    string HallName,
    DateOnly PoolDate,
    bool IsFinalized,
    Guid? WinnerUserId,
    string? WinnerDisplayName,
    int ParticipantCount,
    int TotalSessions,
    DateTimeOffset? FinalizedAtUtc,
    IReadOnlyList<HallDayCompetitionEntryResponse> Entries
);
