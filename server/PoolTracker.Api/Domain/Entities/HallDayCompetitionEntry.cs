namespace PoolTracker.Api.Domain.Entities;

/// <summary>One player's standing within a <see cref="HallDayCompetition"/>.</summary>
public sealed class HallDayCompetitionEntry
{
    public Guid Id { get; set; }

    public Guid HallDayCompetitionId { get; set; }

    public Guid UserId { get; set; }

    public int Rank { get; set; }

    public int GamesWon { get; set; }

    public int GamesLost { get; set; }

    public int BallsPotted { get; set; }

    public int SessionsCompleted { get; set; }

    public int MinutesPlayed { get; set; }

    public HallDayCompetition Competition { get; set; } = null!;

    public ApplicationUser User { get; set; } = null!;
}
