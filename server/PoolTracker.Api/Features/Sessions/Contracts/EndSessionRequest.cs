using System.ComponentModel.DataAnnotations;
using PoolTracker.Api.Domain.Entities;

namespace PoolTracker.Api.Features.Sessions.Contracts;

public sealed class EndSessionRequest
{
    /// <summary>Per-game log captured during the session, in the order the games were played.</summary>
    [MaxLength(200)]
    public IReadOnlyList<GameLogEntry> Games { get; init; } = Array.Empty<GameLogEntry>();

    [MaxLength(750)]
    public string? Notes { get; init; }
}

public sealed class GameLogEntry : IValidatableObject
{
    [EnumDataType(typeof(GameType))]
    public GameType GameType { get; init; }

    [EnumDataType(typeof(BattleType))]
    public BattleType BattleType { get; init; }

    public bool BrokeThisRack { get; init; }

    /// <summary>Balls potted on the break shot. Must be 0 when the player did not break.</summary>
    [Range(0, 50)]
    public int BreakPots { get; init; }

    /// <summary>Balls potted during normal play, excluding the break shot.</summary>
    [Range(0, 50)]
    public int BallsPotted { get; init; }

    [Range(0, 50)]
    public int SnookersFaced { get; init; }

    [Range(0, 50)]
    public int SnookersEscaped { get; init; }

    public bool Won { get; init; }

    public bool GoldenBreak { get; init; }

    /// <summary>
    /// True when this rack ended on a 9-/10-ball train (money ball potted early). Combined with
    /// <see cref="Won"/>: a win hard-sets accuracy to +0.5, a loss to -0.5. 9-/10-ball only.
    /// </summary>
    public bool PottedTrain { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!BrokeThisRack && BreakPots > 0)
        {
            yield return new ValidationResult(
                "Break pots must be 0 when you did not break this rack.",
                new[] { nameof(BreakPots) });
        }

        if (SnookersEscaped > SnookersFaced)
        {
            yield return new ValidationResult(
                "Snookers escaped cannot exceed snookers faced.",
                new[] { nameof(SnookersEscaped) });
        }

        if (GameType == GameType.NineBall && BattleType != BattleType.OneVsOne)
        {
            yield return new ValidationResult(
                "9-ball is singles only and cannot be a 2v2 battle.",
                new[] { nameof(BattleType) });
        }

        if (PottedTrain && GameType == GameType.EightBall)
        {
            yield return new ValidationResult(
                "A train can only be potted in 9-ball or 10-ball.",
                new[] { nameof(PottedTrain) });
        }

        if (GoldenBreak && Won && !BrokeThisRack)
        {
            yield return new ValidationResult(
                "A golden-break win requires breaking the rack.",
                new[] { nameof(GoldenBreak) });
        }

        if (GoldenBreak && !Won && BrokeThisRack)
        {
            yield return new ValidationResult(
                "A golden-break loss means the opponent broke, so you cannot have broken this rack.",
                new[] { nameof(GoldenBreak) });
        }
    }
}
