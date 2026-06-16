using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PoolTracker.Api.Data;
using PoolTracker.Api.Domain.Entities;
using PoolTracker.Api.Services;
using PoolTracker.Api.Tests.Infrastructure;

namespace PoolTracker.Api.Tests;

public sealed class PoolDayEngineTests : IntegrationTestBase
{
    public PoolDayEngineTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task RunAsync_AutoStopsSessionPastIdleCap()
    {
        // Idle cap is 6h; "now" is noon, session opened at 05:00 → stopped at 11:00.
        Factory.Clock.Freeze(new DateTimeOffset(2026, 6, 16, 12, 0, 0, TimeSpan.Zero));

        var owner = await RegisterAndLoginAsync("IdleOwner");
        var hallId = await CreateHallAsync(owner.UserId, "Idle Hall");
        var sessionId = await CreateActiveSessionAsync(owner.UserId, hallId, new DateTimeOffset(2026, 6, 16, 5, 0, 0, TimeSpan.Zero));

        await RunEngineAsync();

        var session = await Factory.ExecuteDbContextAsync(dbContext => dbContext.Sessions
            .Include(current => current.Report)
            .SingleAsync(current => current.Id == sessionId));

        Assert.False(session.IsActive);
        Assert.Equal(SessionEndReason.AutoIdle, session.EndReason);
        Assert.Equal(new DateTimeOffset(2026, 6, 16, 11, 0, 0, TimeSpan.Zero), session.EndedAtUtc);
        Assert.NotNull(session.Report);
        Assert.Equal(0, session.Report!.GamesWon);
    }

    [Fact]
    public async Task RunAsync_AutoStopsSessionAtDayBoundary()
    {
        // "now" is 05:00, one hour past the 04:00 boundary. A session opened at 03:30 (yesterday's
        // pool day, but under the idle cap) is closed exactly at the boundary.
        Factory.Clock.Freeze(new DateTimeOffset(2026, 6, 16, 5, 0, 0, TimeSpan.Zero));

        var owner = await RegisterAndLoginAsync("BoundaryOwner");
        var hallId = await CreateHallAsync(owner.UserId, "Boundary Hall");
        var sessionId = await CreateActiveSessionAsync(owner.UserId, hallId, new DateTimeOffset(2026, 6, 16, 3, 30, 0, TimeSpan.Zero));

        await RunEngineAsync();

        var session = await Factory.ExecuteDbContextAsync(dbContext => dbContext.Sessions
            .SingleAsync(current => current.Id == sessionId));

        Assert.False(session.IsActive);
        Assert.Equal(SessionEndReason.AutoDayClose, session.EndReason);
        Assert.Equal(new DateTimeOffset(2026, 6, 16, 4, 0, 0, TimeSpan.Zero), session.EndedAtUtc);
    }

    [Fact]
    public async Task RunAsync_LeavesFreshSessionActive()
    {
        Factory.Clock.Freeze(new DateTimeOffset(2026, 6, 16, 12, 0, 0, TimeSpan.Zero));

        var owner = await RegisterAndLoginAsync("FreshOwner");
        var hallId = await CreateHallAsync(owner.UserId, "Fresh Hall");
        var sessionId = await CreateActiveSessionAsync(owner.UserId, hallId, new DateTimeOffset(2026, 6, 16, 11, 30, 0, TimeSpan.Zero));

        await RunEngineAsync();

        var session = await Factory.ExecuteDbContextAsync(dbContext => dbContext.Sessions
            .SingleAsync(current => current.Id == sessionId));

        Assert.True(session.IsActive);
        Assert.Null(session.EndReason);
    }

    [Fact]
    public async Task RunAsync_FinalizesClosedDay_WithMostGamesWinner()
    {
        Factory.Clock.Freeze(new DateTimeOffset(2026, 6, 16, 12, 0, 0, TimeSpan.Zero));

        var winner = await RegisterAndLoginAsync("DayWinner");
        var runnerUp = await RegisterAndLoginAsync("DayRunnerUp");
        var hallId = await CreateHallAsync(winner.UserId, "Champion Hall");

        // Yesterday's pool day (2026-06-15) sessions.
        await CreateEndedSessionAsync(winner.UserId, hallId,
            new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 15, 11, 0, 0, TimeSpan.Zero),
            gamesWon: 5, gamesLost: 1, ballsPotted: 30);
        await CreateEndedSessionAsync(runnerUp.UserId, hallId,
            new DateTimeOffset(2026, 6, 15, 14, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 15, 14, 45, 0, TimeSpan.Zero),
            gamesWon: 2, gamesLost: 2, ballsPotted: 18);

        await RunEngineAsync();

        var competition = await Factory.ExecuteDbContextAsync(dbContext => dbContext.HallDayCompetitions
            .Include(current => current.Entries)
            .SingleAsync(current => current.PoolHallId == hallId && current.PoolDate == new DateOnly(2026, 6, 15)));

        Assert.Equal(winner.UserId, competition.WinnerUserId);
        Assert.Equal(5, competition.WinnerGamesWon);
        Assert.Equal(2, competition.ParticipantCount);
        Assert.Equal(2, competition.Entries.Count);

        var winnerEntry = competition.Entries.Single(entry => entry.UserId == winner.UserId);
        var runnerUpEntry = competition.Entries.Single(entry => entry.UserId == runnerUp.UserId);
        Assert.Equal(1, winnerEntry.Rank);
        Assert.Equal(2, runnerUpEntry.Rank);

        // Winner receives the configured bonus (default 25); these DB-seeded sessions award no other points.
        var winnerPoints = await GetPointsAsync(winner.UserId);
        Assert.Equal(25, winnerPoints);
        var runnerUpPoints = await GetPointsAsync(runnerUp.UserId);
        Assert.Equal(0, runnerUpPoints);
    }

    [Fact]
    public async Task RunAsync_FinalizeIsIdempotent()
    {
        Factory.Clock.Freeze(new DateTimeOffset(2026, 6, 16, 12, 0, 0, TimeSpan.Zero));

        var winner = await RegisterAndLoginAsync("IdemWinner");
        var hallId = await CreateHallAsync(winner.UserId, "Idempotent Hall");
        await CreateEndedSessionAsync(winner.UserId, hallId,
            new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 15, 11, 0, 0, TimeSpan.Zero),
            gamesWon: 3, gamesLost: 0, ballsPotted: 12);

        await RunEngineAsync();
        await RunEngineAsync();

        var count = await Factory.ExecuteDbContextAsync(dbContext => dbContext.HallDayCompetitions
            .CountAsync(current => current.PoolHallId == hallId && current.PoolDate == new DateOnly(2026, 6, 15)));
        Assert.Equal(1, count);

        // Bonus must not be paid twice.
        Assert.Equal(25, await GetPointsAsync(winner.UserId));
    }

    [Fact]
    public async Task RunAsync_FinalizesWithoutWinner_WhenNoGamesWon()
    {
        Factory.Clock.Freeze(new DateTimeOffset(2026, 6, 16, 12, 0, 0, TimeSpan.Zero));

        var player = await RegisterAndLoginAsync("NoWinPlayer");
        var hallId = await CreateHallAsync(player.UserId, "Practice Hall");
        await CreateEndedSessionAsync(player.UserId, hallId,
            new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 15, 11, 0, 0, TimeSpan.Zero),
            gamesWon: 0, gamesLost: 0, ballsPotted: 8);

        await RunEngineAsync();

        var competition = await Factory.ExecuteDbContextAsync(dbContext => dbContext.HallDayCompetitions
            .SingleAsync(current => current.PoolHallId == hallId && current.PoolDate == new DateOnly(2026, 6, 15)));

        Assert.Null(competition.WinnerUserId);
        Assert.Equal(1, competition.ParticipantCount);
        Assert.Equal(0, await GetPointsAsync(player.UserId));
    }

    [Fact]
    public async Task RunAsync_DoesNotFinalizeCurrentDay()
    {
        Factory.Clock.Freeze(new DateTimeOffset(2026, 6, 16, 12, 0, 0, TimeSpan.Zero));

        var player = await RegisterAndLoginAsync("TodayPlayer");
        var hallId = await CreateHallAsync(player.UserId, "Today Hall");
        await CreateEndedSessionAsync(player.UserId, hallId,
            new DateTimeOffset(2026, 6, 16, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 16, 10, 0, 0, TimeSpan.Zero),
            gamesWon: 4, gamesLost: 0, ballsPotted: 20);

        await RunEngineAsync();

        var exists = await Factory.ExecuteDbContextAsync(dbContext => dbContext.HallDayCompetitions
            .AnyAsync(current => current.PoolHallId == hallId && current.PoolDate == new DateOnly(2026, 6, 16)));
        Assert.False(exists);
    }

    [Fact]
    public async Task ComputeStandingsAsync_RanksLivePlayersByGamesWon()
    {
        Factory.Clock.Freeze(new DateTimeOffset(2026, 6, 16, 12, 0, 0, TimeSpan.Zero));

        var leader = await RegisterAndLoginAsync("LiveLeader");
        var chaser = await RegisterAndLoginAsync("LiveChaser");
        var hallId = await CreateHallAsync(leader.UserId, "Live Hall");

        await CreateEndedSessionAsync(chaser.UserId, hallId,
            new DateTimeOffset(2026, 6, 16, 8, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 16, 9, 0, 0, TimeSpan.Zero),
            gamesWon: 1, gamesLost: 3, ballsPotted: 9);
        await CreateEndedSessionAsync(leader.UserId, hallId,
            new DateTimeOffset(2026, 6, 16, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 16, 10, 0, 0, TimeSpan.Zero),
            gamesWon: 6, gamesLost: 1, ballsPotted: 25);

        var standings = await Factory.ExecuteScopedAsync(provider =>
        {
            var engine = provider.GetRequiredService<IPoolDayEngine>();
            return engine.ComputeStandingsAsync(hallId, new DateOnly(2026, 6, 16), CancellationToken.None);
        });

        Assert.Equal(2, standings.Count);
        Assert.Equal(leader.UserId, standings[0].UserId);
        Assert.Equal(1, standings[0].Rank);
        Assert.Equal(6, standings[0].GamesWon);
        Assert.Equal(chaser.UserId, standings[1].UserId);
    }

    private Task RunEngineAsync()
    {
        return Factory.ExecuteScopedAsync(provider =>
        {
            var engine = provider.GetRequiredService<IPoolDayEngine>();
            return engine.RunAsync(CancellationToken.None);
        });
    }

    private Task<int> GetPointsAsync(Guid userId)
    {
        return Factory.ExecuteDbContextAsync(async dbContext =>
        {
            var profile = await dbContext.PlayerProfiles.SingleOrDefaultAsync(current => current.UserId == userId);
            return profile?.Points ?? 0;
        });
    }

    private Task<Guid> CreateHallAsync(Guid addedByUserId, string name)
    {
        return Factory.ExecuteDbContextAsync(async dbContext =>
        {
            var hall = new PoolHall
            {
                Id = Guid.NewGuid(),
                Name = name,
                Address = "Engine Address",
                Latitude = 44.8,
                Longitude = 20.5,
                TotalTables = 8,
                AddedByUserId = addedByUserId
            };

            dbContext.PoolHalls.Add(hall);
            await dbContext.SaveChangesAsync();
            return hall.Id;
        });
    }

    private Task<Guid> CreateActiveSessionAsync(Guid userId, Guid hallId, DateTimeOffset startedAtUtc)
    {
        return Factory.ExecuteDbContextAsync(async dbContext =>
        {
            var session = new Session
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PoolHallId = hallId,
                StartedAtUtc = startedAtUtc,
                IsActive = true
            };

            dbContext.Sessions.Add(session);
            await dbContext.SaveChangesAsync();
            return session.Id;
        });
    }

    private Task CreateEndedSessionAsync(
        Guid userId,
        Guid hallId,
        DateTimeOffset startedAtUtc,
        DateTimeOffset endedAtUtc,
        int gamesWon,
        int gamesLost,
        int ballsPotted)
    {
        return Factory.ExecuteDbContextAsync(async dbContext =>
        {
            var session = new Session
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PoolHallId = hallId,
                StartedAtUtc = startedAtUtc,
                EndedAtUtc = endedAtUtc,
                IsActive = false,
                EndReason = SessionEndReason.Manual,
                Report = new SessionReport
                {
                    SessionId = Guid.Empty,
                    GamesWon = gamesWon,
                    GamesLost = gamesLost,
                    BallsPotted = ballsPotted,
                    SnookersEscaped = 0,
                    FlaggedForValidation = false,
                    SubmittedAtUtc = endedAtUtc
                }
            };

            session.Report!.SessionId = session.Id;
            dbContext.Sessions.Add(session);
            await dbContext.SaveChangesAsync();
        });
    }
}
