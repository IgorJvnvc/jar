namespace PoolTracker.Api.Features.Players.Contracts;

public sealed record LeaderboardEntryResponse(
    Guid UserId,
    string DisplayName,
    string AvatarColorHex,
    int Points,
    int TotalGamesPlayed,
    int TotalGamesWon,
    int TotalGamesLost,
    decimal WinRate,
    int TotalBallsPotted,
    int TotalSessions,
    string? Title
);
