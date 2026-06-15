namespace PoolTracker.Api.Domain.Entities;

public sealed class DuelCoinFlip
{
    public Guid Id { get; set; }

    public Guid DuelId { get; set; }

    public Guid FirstChooserUserId { get; set; }

    public Guid? SecondChooserUserId { get; set; }

    public CoinSide? FirstChooserSide { get; set; }

    public CoinSide? SecondChooserSide { get; set; }

    public CoinSide? ResultSide { get; set; }

    public Guid? WinnerUserId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ResolvedAtUtc { get; set; }

    public Duel Duel { get; set; } = null!;

    public ApplicationUser FirstChooserUser { get; set; } = null!;

    public ApplicationUser? SecondChooserUser { get; set; }

    public ApplicationUser? WinnerUser { get; set; }
}
