namespace PoolTracker.Api.Features.Duels.Contracts;

public sealed record DuelStatusResponse(
    Guid Id,
    Guid ChallengerId,
    string ChallengerDisplayName,
    Guid OpponentId,
    string OpponentDisplayName,
    DuelStatusView Status,
    int PointsWager,
    int CurrentRound,
    DuelResultChoiceView? MyLatestChoice,
    DuelResultChoiceView? OpponentLatestChoice,
    bool IsWaitingForMyChoice,
    DuelCoinFlipResponse? CoinFlip,
    Guid? WinnerUserId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc
);
