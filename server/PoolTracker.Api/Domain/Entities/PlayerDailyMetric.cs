namespace PoolTracker.Api.Domain.Entities;

public sealed class PlayerDailyMetric
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public DateOnly Date { get; set; }

    public int TotalBallsPotted { get; set; }

    public int SessionsCompleted { get; set; }

    public int TotalGamesWon { get; set; }

    public int TotalGamesLost { get; set; }

    public ApplicationUser User { get; set; } = null!;
}
