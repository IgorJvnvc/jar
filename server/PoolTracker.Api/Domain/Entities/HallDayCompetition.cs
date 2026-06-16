namespace PoolTracker.Api.Domain.Entities;

/// <summary>
/// A finalized per-hall, per-pool-day competition. Created by the background pool-day engine
/// once a pool day has closed, capturing the winner and the standings snapshot.
/// </summary>
public sealed class HallDayCompetition
{
    public Guid Id { get; set; }

    public Guid PoolHallId { get; set; }

    public DateOnly PoolDate { get; set; }

    public Guid? WinnerUserId { get; set; }

    public int WinnerGamesWon { get; set; }

    public int WinnerBallsPotted { get; set; }

    public int ParticipantCount { get; set; }

    public int TotalSessions { get; set; }

    public DateTimeOffset FinalizedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public PoolHall PoolHall { get; set; } = null!;

    public ApplicationUser? WinnerUser { get; set; }

    public ICollection<HallDayCompetitionEntry> Entries { get; set; } = new List<HallDayCompetitionEntry>();
}
