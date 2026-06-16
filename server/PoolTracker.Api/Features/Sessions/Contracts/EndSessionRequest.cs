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
