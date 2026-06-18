namespace PoolTracker.Api.Features.Players.Contracts;

public sealed record DuelLeaderboardEntryResponse(
    Guid UserId,
    string DisplayName,
    string AvatarColorHex,
    int DuelsWon,
    int DuelsLost,
    int DuelsPlayed,
    decimal WinRate,
    int Points,
    string? Title,
    int Level,
    string LevelTitle
);
