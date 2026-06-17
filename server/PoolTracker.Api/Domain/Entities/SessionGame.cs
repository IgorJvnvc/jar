namespace PoolTracker.Api.Domain.Entities;

public enum GameType
{
    EightBall = 1,
    NineBall = 2,
    TenBall = 3
}

/// <summary>
/// Whether the rack was a singles (1v1) or doubles (2v2) battle. Drives the accuracy
/// scoring table. Defaults to <see cref="OneVsOne"/> so legacy payloads remain valid.
/// 9-ball is singles-only.
/// </summary>
public enum BattleType
{
    OneVsOne = 0,
    TwoVsTwo = 1
}

/// <summary>
/// A single rack/game logged within a play session. Stores the raw per-game inputs the
/// skill calculator consumes; computed skill deltas are aggregated onto the SessionReport.
/// </summary>
public sealed class SessionGame
{
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }

    /// <summary>1-based order in which the game was played within the session.</summary>
    public int Sequence { get; set; }

    public GameType GameType { get; set; }

    /// <summary>Singles (1v1) or doubles (2v2). 9-ball is always <see cref="BattleType.OneVsOne"/>.</summary>
    public BattleType BattleType { get; set; }

    /// <summary>True when this player broke the rack.</summary>
    public bool BrokeThisRack { get; set; }

    /// <summary>Balls potted on the break shot. Always 0 when the player did not break.</summary>
    public int BreakPots { get; set; }

    /// <summary>Balls potted during normal play, excluding the break shot.</summary>
    public int BallsPotted { get; set; }

    /// <summary>Number of times the player was snookered during the game.</summary>
    public int SnookersFaced { get; set; }

    /// <summary>Number of snookers the player successfully escaped.</summary>
    public int SnookersEscaped { get; set; }

    public bool Won { get; set; }

    /// <summary>
    /// True when the game was decided by a golden break. Combined with <see cref="Won"/>:
    /// a win is the player's golden break (override award); a loss means the opponent
    /// golden-broke (neutral, still a recorded loss).
    /// </summary>
    public bool GoldenBreak { get; set; }

    /// <summary>
    /// True when this rack ended on a "train" — the 9-/10-ball money ball potted early (9-ball /
    /// 10-ball only). Because potting the money ball wins the rack, combine with <see cref="Won"/>:
    /// a win means this player potted it (accuracy hard-set to +0.5); a loss means the opponent did
    /// (accuracy hard-set to -0.5). The break bonus and other stats still apply. Always false for 8-ball.
    /// </summary>
    public bool PottedTrain { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public Session Session { get; set; } = null!;
}
