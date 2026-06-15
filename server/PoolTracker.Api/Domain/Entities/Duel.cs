namespace PoolTracker.Api.Domain.Entities;

public enum DuelStatus
{
    Pending = 1,
    Declined = 2,
    Accepted = 3,
    AwaitingSecondReview = 4,
    CoinFlipInProgress = 5,
    Completed = 6,
    Expired = 7
}

public enum DuelPlayerResultChoice
{
    Won = 1,
    Lost = 2
}

public enum CoinSide
{
    Heads = 1,
    Tails = 2
}

public sealed class Duel
{
    public Guid Id { get; set; }

    public Guid ChallengerId { get; set; }

    public Guid OpponentId { get; set; }

    public DuelStatus Status { get; set; } = DuelStatus.Pending;

    public int PointsWager { get; set; } = 100;

    public Guid? WinnerUserId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? RespondedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public ApplicationUser Challenger { get; set; } = null!;

    public ApplicationUser Opponent { get; set; } = null!;

    public ApplicationUser? WinnerUser { get; set; }

    public ICollection<DuelResultSubmission> ResultSubmissions { get; set; } = [];

    public DuelCoinFlip? CoinFlip { get; set; }
}
