using Microsoft.Extensions.Options;
using PoolTracker.Api.Configuration;

namespace PoolTracker.Api.Services;

/// <summary>
/// Translates UTC instants into "pool days". A pool day starts at a configured local boundary
/// hour (e.g. 4am) so a session played past midnight counts toward the day it started.
/// </summary>
public interface IPoolDayClock
{
    /// <summary>The pool date the given instant belongs to.</summary>
    DateOnly GetPoolDate(DateTimeOffset instantUtc);

    /// <summary>The pool date for the current moment.</summary>
    DateOnly CurrentPoolDate();

    /// <summary>UTC instant at which the given pool date begins.</summary>
    DateTimeOffset GetPoolDayStartUtc(DateOnly poolDate);

    /// <summary>UTC instant at which the given pool date ends (start of the next pool date).</summary>
    DateTimeOffset GetPoolDayEndUtc(DateOnly poolDate);
}

public sealed class PoolDayClock : IPoolDayClock
{
    private readonly TimeProvider timeProvider;
    private readonly TimeZoneInfo timeZone;
    private readonly int boundaryHour;

    public PoolDayClock(IOptions<PoolDayOptions> options, TimeProvider timeProvider)
    {
        var value = options.Value;
        this.timeProvider = timeProvider;
        boundaryHour = Math.Clamp(value.DayBoundaryHour, 0, 23);
        timeZone = ResolveTimeZone(value.TimeZoneId);
    }

    public DateOnly GetPoolDate(DateTimeOffset instantUtc)
    {
        var local = TimeZoneInfo.ConvertTime(instantUtc, timeZone);
        var shifted = local.AddHours(-boundaryHour);
        return DateOnly.FromDateTime(shifted.DateTime);
    }

    public DateOnly CurrentPoolDate() => GetPoolDate(timeProvider.GetUtcNow());

    public DateTimeOffset GetPoolDayStartUtc(DateOnly poolDate)
    {
        var localStart = poolDate.ToDateTime(new TimeOnly(boundaryHour, 0));
        var offset = timeZone.GetUtcOffset(localStart);
        return new DateTimeOffset(localStart, offset).ToUniversalTime();
    }

    public DateTimeOffset GetPoolDayEndUtc(DateOnly poolDate) => GetPoolDayStartUtc(poolDate.AddDays(1));

    private static TimeZoneInfo ResolveTimeZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
