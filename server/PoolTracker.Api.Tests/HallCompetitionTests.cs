using System.Net;
using Microsoft.Extensions.DependencyInjection;
using PoolTracker.Api.Domain.Entities;
using PoolTracker.Api.Services;
using PoolTracker.Api.Tests.Infrastructure;

namespace PoolTracker.Api.Tests;

public sealed class HallCompetitionTests : IntegrationTestBase
{
    public HallCompetitionTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task GetCompetition_ForFinalizedDay_ReturnsWinnerAndEntries()
    {
        Factory.Clock.Freeze(new DateTimeOffset(2026, 6, 16, 12, 0, 0, TimeSpan.Zero));

        var winner = await RegisterAndLoginAsync("CompWinner");
        var runnerUp = await RegisterAndLoginAsync("CompRunnerUp");
        var hallId = await CreateHallAsync(winner.UserId, "Result Hall");

        await CreateEndedSessionAsync(winner.UserId, hallId,
            new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 15, 11, 0, 0, TimeSpan.Zero),
            gamesWon: 5, gamesLost: 1, ballsPotted: 30);
        await CreateEndedSessionAsync(runnerUp.UserId, hallId,
            new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 15, 12, 40, 0, TimeSpan.Zero),
            gamesWon: 2, gamesLost: 1, ballsPotted: 15);

        await RunEngineAsync();

        var response = await TestApi.GetAsync(winner, $"/api/halls/{hallId}/competition?date=2026-06-15");
        await TestApi.EnsureStatusAsync(response, HttpStatusCode.OK);
        var competition = await TestApi.ReadAsAsync<HallDayCompetitionResponseDto>(response);

        Assert.True(competition.IsFinalized);
        Assert.Equal(new DateOnly(2026, 6, 15), competition.PoolDate);
        Assert.Equal(winner.UserId, competition.WinnerUserId);
        Assert.Equal(winner.DisplayName, competition.WinnerDisplayName);
        Assert.Equal(2, competition.Entries.Count);
        Assert.Equal(winner.UserId, competition.Entries[0].UserId);
        Assert.Equal(1, competition.Entries[0].Rank);
    }

    [Fact]
    public async Task GetCompetition_ForCurrentDay_ReturnsLiveUnfinalizedStandings()
    {
        Factory.Clock.Freeze(new DateTimeOffset(2026, 6, 16, 12, 0, 0, TimeSpan.Zero));

        var player = await RegisterAndLoginAsync("LivePlayer");
        var hallId = await CreateHallAsync(player.UserId, "Live Result Hall");
        await CreateEndedSessionAsync(player.UserId, hallId,
            new DateTimeOffset(2026, 6, 16, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 16, 10, 0, 0, TimeSpan.Zero),
            gamesWon: 3, gamesLost: 1, ballsPotted: 14);

        var response = await TestApi.GetAsync(player, $"/api/halls/{hallId}/competition");
        await TestApi.EnsureStatusAsync(response, HttpStatusCode.OK);
        var competition = await TestApi.ReadAsAsync<HallDayCompetitionResponseDto>(response);

        Assert.False(competition.IsFinalized);
        Assert.Null(competition.FinalizedAtUtc);
        Assert.Single(competition.Entries);
        Assert.Equal(player.UserId, competition.Entries[0].UserId);
        Assert.Equal(3, competition.Entries[0].GamesWon);
    }

    [Fact]
    public async Task GetCompetition_ForUnknownHall_ReturnsNotFound()
    {
        var player = await RegisterAndLoginAsync("MissingHall");

        var response = await TestApi.GetAsync(player, $"/api/halls/{Guid.NewGuid()}/competition");
        await TestApi.EnsureStatusAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetRecentCompetitions_ReturnsFinalizedResults()
    {
        Factory.Clock.Freeze(new DateTimeOffset(2026, 6, 16, 12, 0, 0, TimeSpan.Zero));

        var winner = await RegisterAndLoginAsync("RecentWinner");
        var hallId = await CreateHallAsync(winner.UserId, "Recent Comp Hall");
        await CreateEndedSessionAsync(winner.UserId, hallId,
            new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 15, 11, 0, 0, TimeSpan.Zero),
            gamesWon: 4, gamesLost: 0, ballsPotted: 22);

        await RunEngineAsync();

        var response = await TestApi.GetAsync(winner, "/api/halls/competitions/recent");
        await TestApi.EnsureStatusAsync(response, HttpStatusCode.OK);
        var competitions = await TestApi.ReadAsAsync<List<HallDayCompetitionResponseDto>>(response);

        var entry = Assert.Single(competitions, current => current.PoolHallId == hallId);
        Assert.True(entry.IsFinalized);
        Assert.Equal(winner.UserId, entry.WinnerUserId);
        Assert.Equal(winner.DisplayName, entry.WinnerDisplayName);
    }

    private Task RunEngineAsync()
    {
        return Factory.ExecuteScopedAsync(provider =>
        {
            var engine = provider.GetRequiredService<IPoolDayEngine>();
            return engine.RunAsync(CancellationToken.None);
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
                Address = "Comp Address",
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
