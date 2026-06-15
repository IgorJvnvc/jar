namespace PoolTracker.Api.Domain.Entities;

public sealed class SessionReport
{
    public Guid SessionId { get; set; }

    public int BallsPotted { get; set; }

    public int GamesWon { get; set; }

    public int GamesLost { get; set; }

    public int SnookersEscaped { get; set; }

    public string? Notes { get; set; }

    public bool FlaggedForValidation { get; set; }

    public DateTimeOffset SubmittedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public Session Session { get; set; } = null!;
}
