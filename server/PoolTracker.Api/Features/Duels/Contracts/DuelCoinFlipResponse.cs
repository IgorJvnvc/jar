namespace PoolTracker.Api.Features.Duels.Contracts;

public sealed record DuelCoinFlipResponse(
    Guid DuelId,
    Guid FirstChooserUserId,
    Guid? SecondChooserUserId,
    CoinSideView? FirstChooserSide,
    CoinSideView? SecondChooserSide,
    CoinSideView? ResultSide,
    Guid? WinnerUserId,
    bool IsWaitingForFirstChooser,
    bool IsWaitingForSecondChooser,
    bool IsResolved
);
