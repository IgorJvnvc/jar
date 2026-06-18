namespace PoolTracker.Api.Services;

/// <summary>
/// Resolved level state for a cumulative experience total. Computed on read — nothing here is
/// persisted, so there is no level/title to keep in sync with <c>Experience</c>.
/// </summary>
public readonly record struct LevelInfo(
    int Level,
    long Experience,
    int ExperienceIntoLevel,
    int ExperienceForNextLevel)
{
    /// <summary>Western-ladder title earned at the current level.</summary>
    public string Title => LevelingMath.GetTitle(Level);
}

/// <summary>
/// Pure leveling math derived solely from a player's cumulative <c>Experience</c>. The curve is a
/// steep, uncapped exponential — each level costs ~1.25x the previous one — so the top of the
/// ladder is a long grind by design. All tuning constants live here.
/// </summary>
public static class LevelingMath
{
    private const double BaseCost = 80d;
    private const double GrowthRate = 1.25d;
    private const int RoundTo = 5;

    /// <summary>XP required to advance from <paramref name="level"/> to the next one.</summary>
    public static int XpToNext(int level)
    {
        if (level < 1)
        {
            level = 1;
        }

        var raw = BaseCost * Math.Pow(GrowthRate, level - 1);

        // Guard against the double overflowing int at absurdly high (unreachable) levels.
        if (raw >= int.MaxValue)
        {
            return int.MaxValue;
        }

        var rounded = (int)(Math.Round(raw / RoundTo) * RoundTo);
        return Math.Max(rounded, RoundTo);
    }

    /// <summary>Resolves level, in-level progress, and cost-to-next for a cumulative XP total.</summary>
    public static LevelInfo GetLevelInfo(long experience)
    {
        var total = experience < 0 ? 0 : experience;
        var level = 1;
        var remaining = total;

        while (true)
        {
            var cost = XpToNext(level);
            if (remaining < cost)
            {
                return new LevelInfo(level, total, (int)remaining, cost);
            }

            remaining -= cost;
            level++;
        }
    }

    /// <summary>Title tier for a level. Greenhorn → Legend of the Felt.</summary>
    public static string GetTitle(int level) => level switch
    {
        >= 40 => "Legend of the Felt",
        >= 30 => "Outlaw",
        >= 20 => "Gunslinger",
        >= 15 => "Sharpshooter",
        >= 10 => "Saloon Regular",
        >= 5 => "Drifter",
        _ => "Greenhorn"
    };
}
