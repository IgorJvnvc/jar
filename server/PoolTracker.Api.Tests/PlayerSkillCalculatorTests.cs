using PoolTracker.Api.Domain.Entities;
using PoolTracker.Api.Services;

namespace PoolTracker.Api.Tests;

public sealed class PlayerSkillCalculatorTests
{
    private readonly PlayerSkillCalculator calculator = new();

    [Fact]
    public void Calculate_WithNoGames_ReturnsEmptyResult()
    {
        var result = calculator.Calculate(Array.Empty<SessionGame>());

        Assert.Equal(0m, result.PowerDelta);
        Assert.Equal(0m, result.AccuracyDelta);
        Assert.Equal(0m, result.CueControlDelta);
        Assert.Equal(0m, result.SpinDelta);
        Assert.Equal(0, result.BallsPotted);
        Assert.Equal(0, result.GamesWon);
        Assert.Equal(0, result.GamesLost);
        Assert.False(result.HasGoldenBreakWin);
    }

    [Theory]
    [InlineData(0, -0.5)]  // missed the break
    [InlineData(1, 0.0)]   // one pot is neutral
    [InlineData(2, 1.0)]   // strong break
    [InlineData(5, 1.0)]   // 2+ is flat
    public void Calculate_Power_ScoresOnlyBrokenRacks(int breakPots, double expected)
    {
        var result = calculator.Calculate(new[]
        {
            Game(broke: true, breakPots: breakPots, ballsPotted: 3, won: true)
        });

        Assert.Equal((decimal)expected, result.PowerDelta);
    }

    [Fact]
    public void Calculate_Power_IgnoresRacksThePlayerDidNotBreak()
    {
        var result = calculator.Calculate(new[]
        {
            Game(broke: false, ballsPotted: 3, won: true)
        });

        Assert.Equal(0m, result.PowerDelta);
    }

    [Theory]
    [InlineData(0, -3.0)]
    [InlineData(1, -2.0)]
    [InlineData(2, -2.0)]
    [InlineData(3, -1.0)]
    [InlineData(4, -0.5)]
    [InlineData(5, 0.5)]
    [InlineData(9, 0.5)]
    public void Calculate_Accuracy_UsesNonBreakPotsEveryGame(int pots, double expected)
    {
        var result = calculator.Calculate(new[]
        {
            Game(broke: false, ballsPotted: pots, won: true)
        });

        Assert.Equal((decimal)expected, result.AccuracyDelta);
    }

    [Fact]
    public void Calculate_CueControl_RewardsWinsAndPenalizesLosses()
    {
        var result = calculator.Calculate(new[]
        {
            Game(ballsPotted: 3, won: true),
            Game(ballsPotted: 3, won: false)
        });

        Assert.Equal(0m, result.CueControlDelta);
        Assert.Equal(1, result.GamesWon);
        Assert.Equal(1, result.GamesLost);
    }

    [Fact]
    public void Calculate_Spin_OnlyScoresSnookeredRacks()
    {
        var escapedAll = calculator.Calculate(new[]
        {
            Game(ballsPotted: 3, snookersFaced: 2, snookersEscaped: 2, won: true)
        });
        Assert.Equal(0.5m, escapedAll.SpinDelta);

        var missedOne = calculator.Calculate(new[]
        {
            Game(ballsPotted: 3, snookersFaced: 2, snookersEscaped: 1, won: true)
        });
        Assert.Equal(-0.5m, missedOne.SpinDelta);

        var notSnookered = calculator.Calculate(new[]
        {
            Game(ballsPotted: 3, snookersFaced: 0, snookersEscaped: 0, won: true)
        });
        Assert.Equal(0m, notSnookered.SpinDelta);
    }

    [Fact]
    public void Calculate_GoldenBreakWin_OverridesFormulaWithFlatBump()
    {
        var result = calculator.Calculate(new[]
        {
            // Despite 0 non-break pots (which would normally be -3 accuracy), the override wins.
            Game(broke: true, breakPots: 1, ballsPotted: 0, won: true, goldenBreak: true)
        });

        Assert.Equal(1m, result.PowerDelta);
        Assert.Equal(1m, result.AccuracyDelta);
        Assert.Equal(1m, result.CueControlDelta);
        Assert.Equal(1m, result.SpinDelta);
        Assert.Equal(1, result.GoldenBreakWins);
        Assert.True(result.HasGoldenBreakWin);
        Assert.Equal(1, result.GamesWon);
    }

    [Fact]
    public void Calculate_GoldenBreakLoss_IsNeutralButCountsAsLoss()
    {
        var result = calculator.Calculate(new[]
        {
            Game(broke: false, ballsPotted: 0, won: false, goldenBreak: true)
        });

        Assert.Equal(0m, result.PowerDelta);
        Assert.Equal(0m, result.AccuracyDelta);
        Assert.Equal(0m, result.CueControlDelta);
        Assert.Equal(0m, result.SpinDelta);
        Assert.Equal(0, result.GoldenBreakWins);
        Assert.Equal(0, result.GamesWon);
        Assert.Equal(1, result.GamesLost);
    }

    [Fact]
    public void Calculate_AggregatesTotalsAcrossGames()
    {
        var result = calculator.Calculate(new[]
        {
            Game(broke: true, breakPots: 2, ballsPotted: 5, snookersFaced: 1, snookersEscaped: 1, won: true),
            Game(broke: false, ballsPotted: 3, snookersFaced: 2, snookersEscaped: 1, won: false)
        });

        Assert.Equal(10, result.BallsPotted);          // (2 + 5) + (0 + 3)
        Assert.Equal(2, result.BallsPottedOnBreak);    // 2 + 0
        Assert.Equal(1, result.GamesWon);
        Assert.Equal(1, result.GamesLost);
        Assert.Equal(1, result.GamesBroken);
        Assert.Equal(3, result.SnookersFaced);         // 1 + 2
        Assert.Equal(2, result.SnookersEscaped);       // 1 + 1
    }

    private static SessionGame Game(
        bool broke = false,
        int breakPots = 0,
        int ballsPotted = 0,
        int snookersFaced = 0,
        int snookersEscaped = 0,
        bool won = false,
        bool goldenBreak = false,
        GameType gameType = GameType.EightBall)
    {
        return new SessionGame
        {
            GameType = gameType,
            BrokeThisRack = broke,
            BreakPots = breakPots,
            BallsPotted = ballsPotted,
            SnookersFaced = snookersFaced,
            SnookersEscaped = snookersEscaped,
            Won = won,
            GoldenBreak = goldenBreak
        };
    }
}
