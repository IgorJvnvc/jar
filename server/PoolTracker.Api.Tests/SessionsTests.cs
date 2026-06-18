using System.Net;
using Microsoft.EntityFrameworkCore;
using PoolTracker.Api.Data;
using PoolTracker.Api.Domain.Entities;
using PoolTracker.Api.Tests.Infrastructure;

namespace PoolTracker.Api.Tests;

public sealed class SessionsTests : IntegrationTestBase
{
    public SessionsTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task StartSession_WithValidHall_ReturnsCreatedActiveSession()
    {
        var session = await RegisterAndLoginAsync("SessionStart");
        var hallId = await CreateHallAsync(session.UserId, "Start Hall");

        var response = await TestApi.PostAsync(session, "/api/sessions/start", new
        {
            poolHallId = hallId,
            poolHallTableId = (Guid?)null
        });

        await TestApi.EnsureStatusAsync(response, HttpStatusCode.Created);
        var payload = await TestApi.ReadAsAsync<SessionResponseDto>(response);

        Assert.True(payload.IsActive);
        Assert.Equal(hallId, payload.PoolHallId);
    }

    [Fact]
    public async Task StartSession_WhenAlreadyActive_ReturnsConflict()
    {
        var session = await RegisterAndLoginAsync("SessionConflict");
        var hallId = await CreateHallAsync(session.UserId, "Conflict Hall");

        var first = await TestApi.PostAsync(session, "/api/sessions/start", new
        {
            poolHallId = hallId,
            poolHallTableId = (Guid?)null
        });
        await TestApi.EnsureStatusAsync(first, HttpStatusCode.Created);

        var second = await TestApi.PostAsync(session, "/api/sessions/start", new
        {
            poolHallId = hallId,
            poolHallTableId = (Guid?)null
        });

        await TestApi.EnsureStatusAsync(second, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task EndSession_StoresReportAndAwardsPoints()
    {
        var session = await RegisterAndLoginAsync("SessionEnd");
        var hallId = await CreateHallAsync(session.UserId, "End Hall");

        var start = await TestApi.PostAsync(session, "/api/sessions/start", new
        {
            poolHallId = hallId,
            poolHallTableId = (Guid?)null
        });
        await TestApi.EnsureStatusAsync(start, HttpStatusCode.Created);
        var started = await TestApi.ReadAsAsync<SessionResponseDto>(start);

        await Factory.ExecuteDbContextAsync(async dbContext =>
        {
            var entity = await dbContext.Sessions.SingleAsync(current => current.Id == started.Id);
            entity.StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-65);
            await dbContext.SaveChangesAsync();
        });

        var end = await TestApi.PostAsync(session, $"/api/sessions/{started.Id}/end", new
        {
            games = new[]
            {
                Game(broke: true, breakPots: 1, ballsPotted: 7, snookersFaced: 1, snookersEscaped: 1, won: true),
                Game(ballsPotted: 6, won: true),
                Game(gameType: "NineBall", broke: true, breakPots: 0, ballsPotted: 5, snookersFaced: 1, snookersEscaped: 1, won: true),
                Game(ballsPotted: 8, won: true),
                Game(broke: true, breakPots: 2, ballsPotted: 4, snookersFaced: 1, snookersEscaped: 1, won: false),
                Game(gameType: "NineBall", ballsPotted: 0, won: false)
            },
            notes = "Great run"
        });

        await TestApi.EnsureStatusAsync(end, HttpStatusCode.OK);
        var payload = await TestApi.ReadAsAsync<SessionResponseDto>(end);

        Assert.False(payload.IsActive);
        Assert.False(payload.IsFlaggedForValidation);
        Assert.True(payload.AwardedPoints >= 20);
        Assert.Equal(4, payload.GamesWon);
        Assert.Equal(2, payload.GamesLost);
        Assert.Equal(3, payload.GamesBroken);
        Assert.Equal(33, payload.BallsPotted);
        Assert.Equal(3, payload.SnookersFaced);
        Assert.Equal(3, payload.SnookersEscaped);
        Assert.Equal(0, payload.GoldenBreaks);

        // Per-rack detail is returned in play order, mirroring the logged racks.
        Assert.Equal(6, payload.Games.Count);
        Assert.Equal(Enumerable.Range(1, 6), payload.Games.Select(game => game.Sequence));

        var firstRack = payload.Games[0];
        Assert.Equal("EightBall", firstRack.GameType);
        Assert.Equal("OneVsOne", firstRack.BattleType);
        Assert.True(firstRack.BrokeThisRack);
        Assert.Equal(1, firstRack.BreakPots);
        Assert.Equal(7, firstRack.BallsPotted);
        Assert.Equal(1, firstRack.SnookersFaced);
        Assert.Equal(1, firstRack.SnookersEscaped);
        Assert.True(firstRack.Won);
        Assert.False(firstRack.PottedTrain);

        Assert.Equal("NineBall", payload.Games[2].GameType);
        Assert.True(payload.Games[2].Won);

        var lastRack = payload.Games[5];
        Assert.Equal("NineBall", lastRack.GameType);
        Assert.False(lastRack.BrokeThisRack);
        Assert.False(lastRack.Won);

        var profile = await GetProfileAsync(session);
        Assert.Equal(payload.AwardedPoints, profile.Points);

        // Session XP: 6 games (+30) + 4 wins (+40) + 33 balls (+33) + 3 snooker escapes (+9)
        // + 13 whole 5-minute blocks of the ~65-minute session (+13) = 125 (more if the run is slow).
        Assert.True(profile.Experience >= 125);
    }

    [Fact]
    public async Task EndSession_PersistsBattleTypeAndTrainInPerRackDetail()
    {
        var session = await RegisterAndLoginAsync("RackDetail");
        var hallId = await CreateHallAsync(session.UserId, "Detail Hall");

        var start = await TestApi.PostAsync(session, "/api/sessions/start", new
        {
            poolHallId = hallId,
            poolHallTableId = (Guid?)null
        });
        await TestApi.EnsureStatusAsync(start, HttpStatusCode.Created);
        var started = await TestApi.ReadAsAsync<SessionResponseDto>(start);

        await Factory.ExecuteDbContextAsync(async dbContext =>
        {
            var entity = await dbContext.Sessions.SingleAsync(current => current.Id == started.Id);
            entity.StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-30);
            await dbContext.SaveChangesAsync();
        });

        var end = await TestApi.PostAsync(session, $"/api/sessions/{started.Id}/end", new
        {
            games = new[]
            {
                Game(gameType: "TenBall", battleType: "TwoVsTwo", broke: true, breakPots: 1, ballsPotted: 5, won: true),
                Game(gameType: "NineBall", ballsPotted: 6, won: true, pottedTrain: true)
            },
            notes = (string?)null
        });

        await TestApi.EnsureStatusAsync(end, HttpStatusCode.OK);
        var payload = await TestApi.ReadAsAsync<SessionResponseDto>(end);

        Assert.Equal(2, payload.Games.Count);

        var doubles = payload.Games[0];
        Assert.Equal(1, doubles.Sequence);
        Assert.Equal("TenBall", doubles.GameType);
        Assert.Equal("TwoVsTwo", doubles.BattleType);
        Assert.True(doubles.BrokeThisRack);
        Assert.False(doubles.PottedTrain);

        var train = payload.Games[1];
        Assert.Equal(2, train.Sequence);
        Assert.Equal("NineBall", train.GameType);
        Assert.Equal("OneVsOne", train.BattleType);
        Assert.True(train.PottedTrain);
    }

    [Fact]
    public async Task EndSession_WithHighPace_FlagsForValidation()
    {
        var session = await RegisterAndLoginAsync("SessionFlag");
        var hallId = await CreateHallAsync(session.UserId, "Flag Hall");

        var start = await TestApi.PostAsync(session, "/api/sessions/start", new
        {
            poolHallId = hallId,
            poolHallTableId = (Guid?)null
        });
        await TestApi.EnsureStatusAsync(start, HttpStatusCode.Created);
        var started = await TestApi.ReadAsAsync<SessionResponseDto>(start);

        await Factory.ExecuteDbContextAsync(async dbContext =>
        {
            var entity = await dbContext.Sessions.SingleAsync(current => current.Id == started.Id);
            entity.StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5);
            await dbContext.SaveChangesAsync();
        });

        var end = await TestApi.PostAsync(session, $"/api/sessions/{started.Id}/end", new
        {
            games = new[]
            {
                Game(ballsPotted: 30, won: true),
                Game(ballsPotted: 30, won: false),
                Game(ballsPotted: 30, won: false),
                Game(ballsPotted: 30, won: false),
                Game(ballsPotted: 30, won: false)
            },
            notes = "speed"
        });

        await TestApi.EnsureStatusAsync(end, HttpStatusCode.OK);
        var payload = await TestApi.ReadAsAsync<SessionResponseDto>(end);

        Assert.True(payload.IsFlaggedForValidation);
    }

    [Fact]
    public async Task GetActiveSession_ReturnsNotFoundWhenNone()
    {
        var session = await RegisterAndLoginAsync("NoActiveSession");

        var response = await TestApi.GetAsync(session, "/api/sessions/active");
        await TestApi.EnsureStatusAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RecentSessions_ReturnsMostRecentEntries()
    {
        var session = await RegisterAndLoginAsync("RecentSessions");
        var hallId = await CreateHallAsync(session.UserId, "Recent Hall");

        for (var i = 0; i < 2; i++)
        {
            var start = await TestApi.PostAsync(session, "/api/sessions/start", new
            {
                poolHallId = hallId,
                poolHallTableId = (Guid?)null
            });
            await TestApi.EnsureStatusAsync(start, HttpStatusCode.Created);
            var started = await TestApi.ReadAsAsync<SessionResponseDto>(start);

            await Factory.ExecuteDbContextAsync(async dbContext =>
            {
                var entity = await dbContext.Sessions.SingleAsync(current => current.Id == started.Id);
                entity.StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-(40 + i * 10));
                await dbContext.SaveChangesAsync();
            });

            var end = await TestApi.PostAsync(session, $"/api/sessions/{started.Id}/end", new
            {
                games = new[]
                {
                    Game(ballsPotted: 10 + i, won: true)
                },
                notes = (string?)null
            });

            await TestApi.EnsureStatusAsync(end, HttpStatusCode.OK);
        }

        var recent = await TestApi.GetAsync(session, "/api/sessions/recent");
        await TestApi.EnsureStatusAsync(recent, HttpStatusCode.OK);
        var payload = await TestApi.ReadAsAsync<List<SessionResponseDto>>(recent);

        Assert.True(payload.Count >= 2);
        Assert.All(payload.Take(2), item => Assert.False(item.IsActive));
    }

    [Fact]
    public async Task EndSession_WithGoldenBreakWin_OverridesStatsAndAwardsBonus()
    {
        var session = await RegisterAndLoginAsync("GoldenWin");
        var hallId = await CreateHallAsync(session.UserId, "Golden Hall");

        var start = await TestApi.PostAsync(session, "/api/sessions/start", new
        {
            poolHallId = hallId,
            poolHallTableId = (Guid?)null
        });
        await TestApi.EnsureStatusAsync(start, HttpStatusCode.Created);
        var started = await TestApi.ReadAsAsync<SessionResponseDto>(start);

        await Factory.ExecuteDbContextAsync(async dbContext =>
        {
            var entity = await dbContext.Sessions.SingleAsync(current => current.Id == started.Id);
            entity.StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-30);
            await dbContext.SaveChangesAsync();
        });

        var end = await TestApi.PostAsync(session, $"/api/sessions/{started.Id}/end", new
        {
            games = new[]
            {
                Game(broke: true, breakPots: 1, won: true, goldenBreak: true)
            },
            notes = "golden"
        });

        await TestApi.EnsureStatusAsync(end, HttpStatusCode.OK);
        var payload = await TestApi.ReadAsAsync<SessionResponseDto>(end);

        // Golden-break wins are auto-flagged and carry a flat 500-point bonus on top of the session award.
        Assert.True(payload.IsFlaggedForValidation);
        Assert.Equal(1, payload.GoldenBreaks);
        Assert.Equal(1, payload.GamesWon);
        Assert.Equal(0, payload.GamesLost);
        Assert.True(payload.AwardedPoints >= 500);

        // Override replaces the normal formula with a flat +1 to every stat (50 -> 51).
        Assert.Equal(1m, payload.PowerDelta);
        Assert.Equal(1m, payload.AccuracyDelta);
        Assert.Equal(1m, payload.CueControlDelta);
        Assert.Equal(1m, payload.SpinDelta);

        var profile = await GetProfileAsync(session);
        Assert.Equal(payload.AwardedPoints, profile.Points);
        Assert.Equal(51m, profile.Power);
        Assert.Equal(51m, profile.Accuracy);
        Assert.Equal(51m, profile.CueControl);
        Assert.Equal(51m, profile.Spin);
    }

    [Fact]
    public async Task EndSession_WithGoldenBreakLoss_IsNeutralButCountsLoss()
    {
        var session = await RegisterAndLoginAsync("GoldenLoss");
        var hallId = await CreateHallAsync(session.UserId, "Golden Loss Hall");

        var start = await TestApi.PostAsync(session, "/api/sessions/start", new
        {
            poolHallId = hallId,
            poolHallTableId = (Guid?)null
        });
        await TestApi.EnsureStatusAsync(start, HttpStatusCode.Created);
        var started = await TestApi.ReadAsAsync<SessionResponseDto>(start);

        await Factory.ExecuteDbContextAsync(async dbContext =>
        {
            var entity = await dbContext.Sessions.SingleAsync(current => current.Id == started.Id);
            entity.StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-30);
            await dbContext.SaveChangesAsync();
        });

        var end = await TestApi.PostAsync(session, $"/api/sessions/{started.Id}/end", new
        {
            games = new[]
            {
                // Opponent golden-broke: the player did not break and the rack is a neutral loss.
                Game(broke: false, won: false, goldenBreak: true)
            },
            notes = (string?)null
        });

        await TestApi.EnsureStatusAsync(end, HttpStatusCode.OK);
        var payload = await TestApi.ReadAsAsync<SessionResponseDto>(end);

        Assert.False(payload.IsFlaggedForValidation);
        Assert.Equal(0, payload.GoldenBreaks);
        Assert.Equal(0, payload.GamesWon);
        Assert.Equal(1, payload.GamesLost);
        Assert.Equal(0m, payload.PowerDelta);
        Assert.Equal(0m, payload.CueControlDelta);

        var profile = await GetProfileAsync(session);
        Assert.Equal(50m, profile.Power);
        Assert.Equal(50m, profile.CueControl);
    }

    [Fact]
    public async Task EndSession_WithNineBallDoubles_ReturnsBadRequest()
    {
        var session = await RegisterAndLoginAsync("NineBallDoubles");
        var hallId = await CreateHallAsync(session.UserId, "Singles Hall");

        var start = await TestApi.PostAsync(session, "/api/sessions/start", new
        {
            poolHallId = hallId,
            poolHallTableId = (Guid?)null
        });
        await TestApi.EnsureStatusAsync(start, HttpStatusCode.Created);
        var started = await TestApi.ReadAsAsync<SessionResponseDto>(start);

        var end = await TestApi.PostAsync(session, $"/api/sessions/{started.Id}/end", new
        {
            games = new[]
            {
                // 9-ball is singles-only; a 2v2 9-ball rack must be rejected.
                Game(gameType: "NineBall", battleType: "TwoVsTwo", ballsPotted: 5, won: true)
            },
            notes = (string?)null
        });

        await TestApi.EnsureStatusAsync(end, HttpStatusCode.BadRequest);
    }

    private static object Game(
        string gameType = "EightBall",
        bool broke = false,
        int breakPots = 0,
        int ballsPotted = 0,
        int snookersFaced = 0,
        int snookersEscaped = 0,
        bool won = false,
        bool goldenBreak = false,
        string battleType = "OneVsOne",
        bool pottedTrain = false)
    {
        return new
        {
            gameType,
            battleType,
            brokeThisRack = broke,
            breakPots,
            ballsPotted,
            snookersFaced,
            snookersEscaped,
            won,
            goldenBreak,
            pottedTrain
        };
    }

    private async Task<Guid> CreateHallAsync(Guid addedByUserId, string name)
    {
        return await Factory.ExecuteDbContextAsync(async dbContext =>
        {
            var hall = new PoolHall
            {
                Id = Guid.NewGuid(),
                Name = name,
                Address = "Test Address",
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

    private static async Task<ProfileResponseDto> GetProfileAsync(TestAuthSession session)
    {
        var response = await TestApi.GetAsync(session, "/api/profile/me");
        await TestApi.EnsureStatusAsync(response, HttpStatusCode.OK);
        return await TestApi.ReadAsAsync<ProfileResponseDto>(response);
    }
}
