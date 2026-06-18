using PoolTracker.Api.Domain.Entities;

namespace PoolTracker.Api.Services;

/// <summary>
/// Computes non-spendable experience awards from gameplay. All XP constants live here so the grind
/// pace can be tuned in one place — mirrors how <see cref="PlayerSkillCalculator"/> centralizes
/// skill scoring.
/// </summary>
public static class ExperienceCalculator
{
    // Session sources.
    private const int PerGamePlayed = 5;
    private const int PerWin = 10;
    private const int PerBallPotted = 1;
    private const int PerSnookerEscaped = 3;
    private const int MinutesPerXp = 5;     // +1 XP per 5 minutes played
    private const int PerGoldenBreak = 50;

    // Duel sources.
    private const int DuelWin = 10;
    private const int DuelLoss = 0;

    /// <summary>
    /// XP earned from a settled session. <paramref name="minutes"/> is the already-capped points
    /// duration so an auto-closed/overnight session can't mint unbounded XP.
    /// </summary>
    public static int ForSession(SkillCalculationResult skills, double minutes)
    {
        var gamesPlayed = skills.GamesWon + skills.GamesLost;
        var durationXp = minutes > 0 ? (int)(minutes / MinutesPerXp) : 0;

        return (gamesPlayed * PerGamePlayed)
            + (skills.GamesWon * PerWin)
            + (skills.BallsPotted * PerBallPotted)
            + (skills.SnookersEscaped * PerSnookerEscaped)
            + durationXp
            + (skills.GoldenBreakWins * PerGoldenBreak);
    }

    /// <summary>XP for a duel participant. Winner earns a small trickle; loser earns nothing.</summary>
    public static int ForDuel(bool isWinner) => isWinner ? DuelWin : DuelLoss;
}
