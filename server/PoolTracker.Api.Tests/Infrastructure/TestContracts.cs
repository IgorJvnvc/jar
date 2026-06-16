namespace PoolTracker.Api.Tests.Infrastructure;

internal sealed record UserSummaryDto(
    Guid Id,
    string DisplayName,
    string Email
);

internal sealed record AuthResponseDto(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    UserSummaryDto User
);

internal sealed record ProfileResponseDto(
    Guid UserId,
    string DisplayName,
    string Email,
    string AvatarColorHex,
    int? FavoriteBallNumber,
    int Points,
    int DebtPoints,
    string? Title,
    decimal Power,
    decimal Accuracy,
    decimal CueControl,
    decimal Spin,
    DateTimeOffset UpdatedAtUtc
);

internal sealed record PayDebtResponseDto(
    int PaidPoints,
    ProfileResponseDto Profile
);

internal sealed record SessionResponseDto(
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

internal sealed record PoolHallResponseDto(
    Guid Id,
    string Name,
    string Address,
    double Latitude,
    double Longitude,
    int TotalTables,
    decimal OverallScore,
    int RatingsCount,
    DateTimeOffset CreatedAtUtc
);

internal sealed record PoolHallTableResponseDto(
    Guid Id,
    string TableLabel,
    decimal OverallScore,
    int RatingsCount
);

internal sealed record PoolHallDetailResponseDto(
    Guid Id,
    string Name,
    string Address,
    double Latitude,
    double Longitude,
    int TotalTables,
    decimal OverallScore,
    int RatingsCount,
    IReadOnlyList<PoolHallTableResponseDto> Tables
);

internal sealed record ActiveSessionPlayerResponseDto(
    Guid UserId,
    string DisplayName,
    string AvatarColorHex,
    Guid SessionId,
    Guid PoolHallId,
    string PoolHallName,
    Guid? PoolHallTableId,
    string? PoolHallTableLabel,
    DateTimeOffset StartedAtUtc
);

internal sealed record PlayerListItemResponseDto(
    Guid UserId,
    string DisplayName,
    string Email,
    string AvatarColorHex,
    int Points,
    int DebtPoints,
    string? Title
);

internal sealed record LeaderboardEntryResponseDto(
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

internal sealed record CueItemResponseDto(
    Guid Id,
    string Name,
    string ColorHex,
    string Rarity,
    int? ShopCost,
    string? AchievementCode,
    decimal PowerBonus,
    decimal AccuracyBonus,
    decimal CueControlBonus,
    decimal SpinBonus,
    bool IsOwned,
    bool IsEquipped
);

internal sealed record DuelCoinFlipResponseDto(
    Guid DuelId,
    Guid FirstChooserUserId,
    Guid? SecondChooserUserId,
    string? FirstChooserSide,
    string? SecondChooserSide,
    string? ResultSide,
    Guid? WinnerUserId,
    bool IsWaitingForFirstChooser,
    bool IsWaitingForSecondChooser,
    bool IsResolved
);

internal sealed record DuelStatusResponseDto(
    Guid Id,
    Guid ChallengerId,
    string ChallengerDisplayName,
    Guid OpponentId,
    string OpponentDisplayName,
    string Status,
    int PointsWager,
    int CurrentRound,
    string? MyLatestChoice,
    string? OpponentLatestChoice,
    bool IsWaitingForMyChoice,
    DuelCoinFlipResponseDto? CoinFlip,
    Guid? WinnerUserId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc
);

internal sealed record DuelListResponseDto(
    IReadOnlyList<DuelStatusResponseDto> Items
);

internal sealed record DeviceTokenRowDto(
    Guid UserId,
    string Token,
    string Platform
);

internal sealed class ErrorEnvelope
{
    public string? Message { get; set; }
}
