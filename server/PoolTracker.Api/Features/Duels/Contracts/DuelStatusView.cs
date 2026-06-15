namespace PoolTracker.Api.Features.Duels.Contracts;

public enum DuelStatusView
{
    Pending = 1,
    Declined = 2,
    Accepted = 3,
    AwaitingSecondReview = 4,
    CoinFlipInProgress = 5,
    Completed = 6,
    Expired = 7
}
