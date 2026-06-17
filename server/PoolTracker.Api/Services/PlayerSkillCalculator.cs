using PoolTracker.Api.Domain.Entities;

namespace PoolTracker.Api.Services;

/// <summary>
/// Skill deltas and session totals computed from the per-game log. Deltas are fractional and
/// summed across all games in the session, then applied (clamped 0-100) to the player's stored
/// profile stats at settlement. All scoring constants live here so they can be tuned in one place.
/// </summary>
public sealed record SkillCalculationResult(
    decimal PowerDelta,
    decimal AccuracyDelta,
    decimal CueControlDelta,
    decimal SpinDelta,
    int BallsPotted,
    int BallsPottedOnBreak,
    int GamesWon,
    int GamesLost,
    int GamesBroken,
    int SnookersFaced,
    int SnookersEscaped,
    int GoldenBreakWins)
{
    /// <summary>Result for a session with no logged games (e.g. an auto-closed session).</summary>
    public static SkillCalculationResult Empty { get; } = new(0m, 0m, 0m, 0m, 0, 0, 0, 0, 0, 0, 0, 0);

    public bool HasGoldenBreakWin => GoldenBreakWins > 0;
}

public interface IPlayerSkillCalculator
{
    SkillCalculationResult Calculate(IReadOnlyList<SessionGame> games);
}

public sealed class PlayerSkillCalculator : IPlayerSkillCalculator
{
    // Power (break pots) — only applied on racks the player broke.
    private const decimal PowerBreakMiss = -0.5m;    // 0 pots on the break
    private const decimal PowerBreakNeutral = 0m;    // 1 pot on the break
    private const decimal PowerBreakStrong = 1m;     // 2+ pots on the break

    // Accuracy (non-break pots) — applied every game. The thresholds depend on the game type and
    // battle type (singles vs doubles); see AccuracyForGame for the per-mode tables.
    private const decimal AccuracyPenaltyBig = -2m;
    private const decimal AccuracyPenaltySmall = -1m;
    private const decimal AccuracyNeutral = 0m;
    private const decimal AccuracyBonusSmall = 1m;
    private const decimal AccuracyBonusMid = 1.5m;
    private const decimal AccuracyBonusBig = 2m;

    // Cue control (result) — applied every game.
    private const decimal CueControlWin = 0.5m;
    private const decimal CueControlLoss = -0.5m;

    // Spin (snooker escapes) — only applied on racks where the player was snookered.
    private const decimal SpinEscapedAll = 0.5m;
    private const decimal SpinMissedAny = -0.5m;

    // Golden-break win override — flat bump to every stat, replacing the normal formula.
    private const decimal GoldenBreakBump = 1m;

    public SkillCalculationResult Calculate(IReadOnlyList<SessionGame> games)
    {
        decimal power = 0m, accuracy = 0m, cueControl = 0m, spin = 0m;
        int ballsPotted = 0, ballsOnBreak = 0, gamesWon = 0, gamesLost = 0;
        int gamesBroken = 0, snookersFaced = 0, snookersEscaped = 0, goldenWins = 0;

        foreach (var game in games)
        {
            // Standings/aggregate totals are always recorded from the raw inputs.
            ballsPotted += game.BreakPots + game.BallsPotted;
            ballsOnBreak += game.BreakPots;
            snookersFaced += game.SnookersFaced;
            snookersEscaped += game.SnookersEscaped;
            if (game.BrokeThisRack)
            {
                gamesBroken++;
            }

            if (game.Won)
            {
                gamesWon++;
            }
            else
            {
                gamesLost++;
            }

            if (game.GoldenBreak)
            {
                if (game.Won)
                {
                    // Override: flat bump to all four stats; the normal formula is skipped.
                    power += GoldenBreakBump;
                    accuracy += GoldenBreakBump;
                    cueControl += GoldenBreakBump;
                    spin += GoldenBreakBump;
                    goldenWins++;
                }

                // A golden-break loss is neutral: it still counts as a loss above, but no skill deltas.
                continue;
            }

            // Power — gated on the player breaking this rack.
            if (game.BrokeThisRack)
            {
                power += PowerForBreak(game.BreakPots);
            }

            // Accuracy — every game, from non-break pots, scored per game/battle type.
            accuracy += AccuracyForGame(game.GameType, game.BattleType, game.BallsPotted, game.PottedTrain);

            // Cue control — every game, from the result.
            cueControl += game.Won ? CueControlWin : CueControlLoss;

            // Spin — gated on being snookered this rack.
            if (game.SnookersFaced > 0)
            {
                spin += game.SnookersEscaped >= game.SnookersFaced ? SpinEscapedAll : SpinMissedAny;
            }
        }

        return new SkillCalculationResult(
            power,
            accuracy,
            cueControl,
            spin,
            ballsPotted,
            ballsOnBreak,
            gamesWon,
            gamesLost,
            gamesBroken,
            snookersFaced,
            snookersEscaped,
            goldenWins);
    }

    private static decimal PowerForBreak(int breakPots)
    {
        if (breakPots <= 0)
        {
            return PowerBreakMiss;
        }

        return breakPots == 1 ? PowerBreakNeutral : PowerBreakStrong;
    }

    private static decimal AccuracyForGame(GameType gameType, BattleType battleType, int pots, bool pottedTrain)
    {
        var delta = (gameType, battleType) switch
        {
            (GameType.EightBall, BattleType.TwoVsTwo) => EightBallDoubles(pots),
            (GameType.EightBall, _) => EightBallSingles(pots),
            (GameType.NineBall, _) => NineBallSingles(pots),       // 9-ball is singles-only
            (GameType.TenBall, BattleType.TwoVsTwo) => TenBallDoubles(pots),
            (GameType.TenBall, _) => TenBallSingles(pots),
            _ => AccuracyNeutral
        };

        // A potted 9-/10-ball train waives a negative accuracy result (no effect when already >= 0).
        if (pottedTrain && delta < 0m)
        {
            return AccuracyNeutral;
        }

        return delta;
    }

    private static decimal EightBallSingles(int pots) => pots switch
    {
        < 3 => AccuracyPenaltySmall,
        3 => AccuracyNeutral,
        _ => AccuracyBonusSmall
    };

    private static decimal EightBallDoubles(int pots) => pots switch
    {
        < 3 => AccuracyPenaltySmall,
        3 => AccuracyNeutral,
        4 => AccuracyBonusSmall,
        5 => AccuracyBonusMid,
        _ => AccuracyBonusBig
    };

    private static decimal NineBallSingles(int pots) => pots switch
    {
        < 4 => AccuracyPenaltyBig,
        4 => AccuracyNeutral,
        _ => AccuracyBonusSmall
    };

    private static decimal TenBallSingles(int pots) => pots switch
    {
        <= 4 => AccuracyPenaltyBig,
        5 => AccuracyNeutral,
        _ => AccuracyBonusSmall
    };

    private static decimal TenBallDoubles(int pots) => pots switch
    {
        < 3 => AccuracyPenaltySmall,
        3 => AccuracyNeutral,
        4 => AccuracyBonusSmall,
        _ => AccuracyBonusMid
    };
}
