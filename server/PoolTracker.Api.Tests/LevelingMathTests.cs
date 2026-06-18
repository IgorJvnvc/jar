using PoolTracker.Api.Services;

namespace PoolTracker.Api.Tests;

public sealed class LevelingMathTests
{
    [Theory]
    [InlineData(1, 80)]
    [InlineData(2, 100)]
    [InlineData(3, 125)]
    [InlineData(4, 155)]
    [InlineData(5, 195)]
    public void XpToNext_FollowsRoundedExponentialCurve(int level, int expected)
    {
        Assert.Equal(expected, LevelingMath.XpToNext(level));
    }

    [Fact]
    public void XpToNext_NeverShrinksBetweenLevels()
    {
        for (var level = 1; level < 40; level++)
        {
            Assert.True(LevelingMath.XpToNext(level + 1) >= LevelingMath.XpToNext(level));
        }
    }

    [Theory]
    [InlineData(0, 1, 0, 80)]
    [InlineData(79, 1, 79, 80)]
    [InlineData(80, 2, 0, 100)]
    [InlineData(179, 2, 99, 100)]
    [InlineData(180, 3, 0, 125)]
    [InlineData(305, 4, 0, 155)]
    [InlineData(460, 5, 0, 195)]
    public void GetLevelInfo_ResolvesLevelAndProgress(long experience, int level, int into, int forNext)
    {
        var info = LevelingMath.GetLevelInfo(experience);

        Assert.Equal(level, info.Level);
        Assert.Equal(into, info.ExperienceIntoLevel);
        Assert.Equal(forNext, info.ExperienceForNextLevel);
        Assert.Equal(experience, info.Experience);
    }

    [Fact]
    public void GetLevelInfo_ClampsNegativeExperienceToLevelOne()
    {
        var info = LevelingMath.GetLevelInfo(-50);

        Assert.Equal(1, info.Level);
        Assert.Equal(0, info.ExperienceIntoLevel);
        Assert.Equal(0, info.Experience);
    }

    [Theory]
    [InlineData(1, "Greenhorn")]
    [InlineData(4, "Greenhorn")]
    [InlineData(5, "Drifter")]
    [InlineData(9, "Drifter")]
    [InlineData(10, "Saloon Regular")]
    [InlineData(14, "Saloon Regular")]
    [InlineData(15, "Sharpshooter")]
    [InlineData(19, "Sharpshooter")]
    [InlineData(20, "Gunslinger")]
    [InlineData(29, "Gunslinger")]
    [InlineData(30, "Outlaw")]
    [InlineData(39, "Outlaw")]
    [InlineData(40, "Legend of the Felt")]
    [InlineData(75, "Legend of the Felt")]
    public void GetTitle_MapsLevelToWesternLadder(int level, string expected)
    {
        Assert.Equal(expected, LevelingMath.GetTitle(level));
    }

    [Fact]
    public void GetLevelInfo_TitleMatchesResolvedLevel()
    {
        // 460 cumulative XP == start of level 5 == Drifter.
        Assert.Equal("Drifter", LevelingMath.GetLevelInfo(460).Title);
    }
}
