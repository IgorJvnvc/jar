namespace PoolTracker.Api.Configuration;

public sealed class PoolDayOptions
{
    public const string SectionName = "PoolDay";

    /// <summary>Time zone the pool community lives in (IANA or Windows id). Falls back to UTC if unknown.</summary>
    public string TimeZoneId { get; set; } = "UTC";

    /// <summary>Local hour [0-23] at which a new pool day begins, so late-night play counts to the day it started.</summary>
    public int DayBoundaryHour { get; set; } = 4;

    /// <summary>A session left open longer than this many hours is auto-stopped.</summary>
    public int IdleCapHours { get; set; } = 6;

    /// <summary>How often the background pool-day sweep runs.</summary>
    public int SweepIntervalMinutes { get; set; } = 15;

    /// <summary>Bonus points awarded to a hall's daily winner.</summary>
    public int HallWinBonusPoints { get; set; } = 25;
}
