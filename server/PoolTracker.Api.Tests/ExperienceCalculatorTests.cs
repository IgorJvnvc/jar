using PoolTracker.Api.Services;

namespace PoolTracker.Api.Tests;

public sealed class ExperienceCalculatorTests
{
    [Fact]
    public void ForSession_WithNoActivity_ReturnsZero()
    {
        Assert.Equal(0, ExperienceCalculator.ForSession(SkillCalculationResult.Empty, 0));
    }

    [Fact]
    public void ForSession_SumsAllSources()
    {
        // 6 games played (3W/3L) + 30 balls + 2 snooker escapes + 60 minutes.
        var skills = SkillCalculationResult.Empty with
        {
            GamesWon = 3,
            GamesLost = 3,
            BallsPotted = 30,
            SnookersEscaped = 2
        };

        // 6*5 + 3*10 + 30*1 + 2*3 + (60/5) = 30 + 30 + 30 + 6 + 12 = 108
        Assert.Equal(108, ExperienceCalculator.ForSession(skills, 60));
    }

    [Fact]
    public void ForSession_RewardsGoldenBreak()
    {
        var skills = SkillCalculationResult.Empty with
        {
            GamesWon = 1,
            GoldenBreakWins = 1
        };

        // 1*5 + 1*10 + 1*50 = 65
        Assert.Equal(65, ExperienceCalculator.ForSession(skills, 0));
    }

    [Fact]
    public void ForSession_FloorsDurationToFiveMinuteBlocks()
    {
        // 14 minutes -> 2 whole 5-minute blocks.
        Assert.Equal(2, ExperienceCalculator.ForSession(SkillCalculationResult.Empty, 14));
    }

    [Theory]
    [InlineData(true, 10)]
    [InlineData(false, 0)]
    public void ForDuel_AwardsWinnerOnly(bool isWinner, int expected)
    {
        Assert.Equal(expected, ExperienceCalculator.ForDuel(isWinner));
    }
}
