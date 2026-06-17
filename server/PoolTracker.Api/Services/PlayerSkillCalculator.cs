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
    // battle type (singles vs doubles); see AccuracyForGame for the per-mode tables. In 9-/10-ball
    // each break pot also adds +1 accuracy (BreakAccuracyBonus), on top of the Power delta.
    private const decimal AccuracyPenaltyBig = -2m;
    private const decimal AccuracyPenaltySmall = -1m;
    private const decimal AccuracyNeutral = 0m;
    private const decimal AccuracyBonusTiny = 0.5m;
    private const decimal AccuracyBonusSmall = 1m;
    private const decimal AccuracyBonusMid = 1.5m;
    private const decimal AccuracyBonusBig = 2m;

    // Train (9-/10-ball) hard-sets the accuracy table component from the result: potting the money
    // ball early and winning is rewarded; losing to an opponent's train softens the penalty. The
    // break bonus, Power, cue control and spin still stack on top.
    private const decimal TrainWinAccuracy = 0.5m;
    private const decimal TrainLossAccuracy = -0.5m;

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

            // Accuracy — table component (with the train override) plus the 9-/10-ball break bonus.
            accuracy += AccuracyForGame(game.GameType, game.BattleType, game.BallsPotted, game.PottedTrain, game.Won);
            accuracy += BreakAccuracyBonus(game.GameType, game.BreakPots);

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

    private static decimal AccuracyForGame(GameType gameType, BattleType battleType, int pots, bool pottedTrain, bool won)
    {
        // A 9-/10-ball train hard-sets the table component: a win is rewarded, a loss is softened.
        // (PottedTrain is only ever true for 9-/10-ball.) The break bonus is added separately.
        if (pottedTrain)
        {
            return won ? TrainWinAccuracy : TrainLossAccuracy;
        }

        return (gameType, battleType) switch
        {
            (GameType.EightBall, BattleType.TwoVsTwo) => EightBallDoubles(pots),
            (GameType.EightBall, _) => EightBallSingles(pots),
            (GameType.NineBall, _) => NineBallSingles(pots),       // 9-ball is singles-only
            (GameType.TenBall, BattleType.TwoVsTwo) => TenBallDoubles(pots),
            (GameType.TenBall, _) => TenBallSingles(pots),
            _ => AccuracyNeutral
        };
    }

    // In 9-/10-ball each ball potted on the break adds +1 accuracy, on top of the Power delta.
    // BreakPots is 0 when the player did not break, so no extra gating is needed.
    private static decimal BreakAccuracyBonus(GameType gameType, int breakPots)
        => gameType is GameType.NineBall or GameType.TenBall ? breakPots : 0m;

    // 8-ball singles: <=4 -> -1, >=5 -> +0.5
    private static decimal EightBallSingles(int pots) => pots switch
    {
        <= 4 => AccuracyPenaltySmall,
        _ => AccuracyBonusTiny
    };

    // 8-ball doubles: <3 -> -1, 3-4 -> +1, >=5 -> +2
    private static decimal EightBallDoubles(int pots) => pots switch
    {
        < 3 => AccuracyPenaltySmall,
        < 5 => AccuracyBonusSmall,
        _ => AccuracyBonusBig
    };

    // 9-ball singles only: <3 -> -2, 3 -> 0, >3 -> +1
    private static decimal NineBallSingles(int pots) => pots switch
    {
        < 3 => AccuracyPenaltyBig,
        3 => AccuracyNeutral,
        _ => AccuracyBonusSmall
    };

    // 10-ball singles: <=4 -> -2, 5 -> +1, >=6 -> +1.5
    private static decimal TenBallSingles(int pots) => pots switch
    {
        <= 4 => AccuracyPenaltyBig,
        5 => AccuracyBonusSmall,
        _ => AccuracyBonusMid
    };

    // 10-ball doubles: <=2 -> -1, 3 -> 0, >3 -> +1
    private static decimal TenBallDoubles(int pots) => pots switch
    {
        <= 2 => AccuracyPenaltySmall,
        3 => AccuracyNeutral,
        _ => AccuracyBonusSmall
    };
}
