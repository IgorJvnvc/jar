using Microsoft.Extensions.Options;
using PoolTracker.Api.Configuration;
using PoolTracker.Api.Services;
using PoolTracker.Api.Tests.Infrastructure;

namespace PoolTracker.Api.Tests;

public sealed class PoolDayClockTests
{
    private static PoolDayClock CreateClock(TimeProvider timeProvider, int boundaryHour = 4, string timeZoneId = "UTC")
    {
        var options = Options.Create(new PoolDayOptions
        {
            TimeZoneId = timeZoneId,
            DayBoundaryHour = boundaryHour
        });

        return new PoolDayClock(options, timeProvider);
    }

    [Fact]
    public void GetPoolDate_JustBeforeBoundary_BelongsToPreviousDay()
    {
        var clock = CreateClock(new TestTimeProvider());

        var poolDate = clock.GetPoolDate(new DateTimeOffset(2026, 6, 16, 3, 59, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 6, 15), poolDate);
    }

    [Fact]
    public void GetPoolDate_AtBoundary_BelongsToNewDay()
    {
        var clock = CreateClock(new TestTimeProvider());

        var poolDate = clock.GetPoolDate(new DateTimeOffset(2026, 6, 16, 4, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 6, 16), poolDate);
    }

    [Fact]
    public void GetPoolDate_Midday_BelongsToSameDay()
    {
        var clock = CreateClock(new TestTimeProvider());

        var poolDate = clock.GetPoolDate(new DateTimeOffset(2026, 6, 16, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 6, 16), poolDate);
    }

    [Fact]
    public void GetPoolDayStartAndEnd_AreBoundaryAligned()
    {
        var clock = CreateClock(new TestTimeProvider());
        var poolDate = new DateOnly(2026, 6, 16);

        Assert.Equal(new DateTimeOffset(2026, 6, 16, 4, 0, 0, TimeSpan.Zero), clock.GetPoolDayStartUtc(poolDate));
        Assert.Equal(new DateTimeOffset(2026, 6, 17, 4, 0, 0, TimeSpan.Zero), clock.GetPoolDayEndUtc(poolDate));
    }

    [Fact]
    public void CurrentPoolDate_UsesFrozenNow()
    {
        var time = new TestTimeProvider();
        time.Freeze(new DateTimeOffset(2026, 6, 16, 2, 0, 0, TimeSpan.Zero));
        var clock = CreateClock(time);

        // 02:00 UTC is before the 04:00 boundary, so it still counts as the previous pool day.
        Assert.Equal(new DateOnly(2026, 6, 15), clock.CurrentPoolDate());
    }

    [Fact]
    public void UnknownTimeZone_FallsBackToUtc()
    {
        var clock = CreateClock(new TestTimeProvider(), timeZoneId: "Not/ARealZone");

        var poolDate = clock.GetPoolDate(new DateTimeOffset(2026, 6, 16, 4, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 6, 16), poolDate);
    }
}
