namespace PoolTracker.Api.Domain.Entities;

public sealed class SessionReport
{
    public Guid SessionId { get; set; }

    public int BallsPotted { get; set; }

    /// <summary>Subset of <see cref="BallsPotted"/> that were potted on break shots.</summary>
    public int BallsPottedOnBreak { get; set; }

    public int GamesWon { get; set; }

    public int GamesLost { get; set; }

    /// <summary>Number of games in the session where the player broke the rack.</summary>
    public int GamesBroken { get; set; }

    public int SnookersEscaped { get; set; }

    /// <summary>Total number of times the player was snookered across the session.</summary>
    public int SnookersFaced { get; set; }

    /// <summary>Number of golden-break wins recorded in the session.</summary>
    public int GoldenBreaks { get; set; }

    public string? Notes { get; set; }

    public bool FlaggedForValidation { get; set; }

    // Audit trail of the skill deltas applied to the player's profile at settlement.
    public decimal PowerDelta { get; set; }

    public decimal AccuracyDelta { get; set; }

    public decimal CueControlDelta { get; set; }

    public decimal SpinDelta { get; set; }

    public DateTimeOffset SubmittedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public Session Session { get; set; } = null!;
}
