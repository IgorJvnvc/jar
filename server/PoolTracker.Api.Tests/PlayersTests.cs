using System.Net;
using Microsoft.EntityFrameworkCore;
using PoolTracker.Api.Domain.Entities;
using PoolTracker.Api.Tests.Infrastructure;

namespace PoolTracker.Api.Tests;

public sealed class PlayersTests : IntegrationTestBase
{
    public PlayersTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task GetPlayers_ReturnsRegisteredUsers()
    {
        var first = await RegisterAndLoginAsync("PlayersA");
        var second = await RegisterAndLoginAsync("PlayersB");

        var response = await TestApi.GetAsync(first, "/api/players");
        await TestApi.EnsureStatusAsync(response, HttpStatusCode.OK);

        var players = await TestApi.ReadAsAsync<List<PlayerListItemResponseDto>>(response);

        Assert.Contains(players, player => player.UserId == first.UserId);
        Assert.Contains(players, player => player.UserId == second.UserId);
    }

    [Fact]
    public async Task GetLeaderboard_RanksByWinRateThenPoints()
    {
        var sharp = await RegisterAndLoginAsync("LeaderSharp");
        var steady = await RegisterAndLoginAsync("LeaderSteady");
        var rookie = await RegisterAndLoginAsync("LeaderRookie");

        var hallId = await CreateHallAsync(sharp.UserId, "Leaderboard Hall");

        // Sharp: 9 wins / 1 loss => 90% win rate, lower points.
        await CreateReportedSessionAsync(sharp.UserId, hallId, gamesWon: 9, gamesLost: 1, ballsPotted: 40);
        await SetPointsAsync(sharp.UserId, 300);

        // Steady: 5 wins / 5 losses => 50% win rate, highest points.
        await CreateReportedSessionAsync(steady.UserId, hallId, gamesWon: 5, gamesLost: 5, ballsPotted: 25);
        await SetPointsAsync(steady.UserId, 900);

        // Rookie: no completed sessions => 0% win rate.
        await SetPointsAsync(rookie.UserId, 50);

        var response = await TestApi.GetAsync(sharp, "/api/players/leaderboard");
        await TestApi.EnsureStatusAsync(response, HttpStatusCode.OK);
        var board = await TestApi.ReadAsAsync<List<LeaderboardEntryResponseDto>>(response);

        var sharpEntry = board.Single(entry => entry.UserId == sharp.UserId);
        var steadyEntry = board.Single(entry => entry.UserId == steady.UserId);
        var rookieEntry = board.Single(entry => entry.UserId == rookie.UserId);

        Assert.Equal(10, sharpEntry.TotalGamesPlayed);
        Assert.Equal(9, sharpEntry.TotalGamesWon);
        Assert.Equal(0.9m, sharpEntry.WinRate);
        Assert.Equal(40, sharpEntry.TotalBallsPotted);
        Assert.Equal(1, sharpEntry.TotalSessions);

        Assert.Equal(0.5m, steadyEntry.WinRate);
        Assert.Equal(0m, rookieEntry.WinRate);
        Assert.Equal(0, rookieEntry.TotalGamesPlayed);

        // Players with no experience sit at level 1 / Greenhorn.
        Assert.Equal(1, sharpEntry.Level);
        Assert.Equal("Greenhorn", sharpEntry.LevelTitle);

        // Highest win rate wins regardless of points; points only break win-rate ties.
        Assert.True(board.IndexOf(sharpEntry) < board.IndexOf(steadyEntry));
        Assert.True(board.IndexOf(steadyEntry) < board.IndexOf(rookieEntry));
    }

    [Fact]
    public async Task GetLeaderboard_IncludesLevelDerivedFromExperience()
    {
        var veteran = await RegisterAndLoginAsync("LeaderVeteran");
        var hallId = await CreateHallAsync(veteran.UserId, "Level Hall");

        await CreateReportedSessionAsync(veteran.UserId, hallId, gamesWon: 5, gamesLost: 5, ballsPotted: 25);
        await SetExperienceAsync(veteran.UserId, 460); // exactly the start of level 5

        var response = await TestApi.GetAsync(veteran, "/api/players/leaderboard");
        await TestApi.EnsureStatusAsync(response, HttpStatusCode.OK);
        var board = await TestApi.ReadAsAsync<List<LeaderboardEntryResponseDto>>(response);

        var entry = board.Single(current => current.UserId == veteran.UserId);
        Assert.Equal(5, entry.Level);
        Assert.Equal("Drifter", entry.LevelTitle);
    }

    [Fact]
    public async Task GetDuelLeaderboard_RanksByWinRate_AndExcludesPlayersWithoutDuels()
    {
        var sharp = await RegisterAndLoginAsync("DuelLeaderSharp");
        var steady = await RegisterAndLoginAsync("DuelLeaderSteady");
        var rookie = await RegisterAndLoginAsync("DuelLeaderRookie");

        // Sharp: 9 wins / 1 loss => 90% win rate, fewer points.
        await SetDuelRecordAsync(sharp.UserId, duelsWon: 9, duelsLost: 1, points: 300);
        // Steady: 5 wins / 5 losses => 50% win rate, highest points.
        await SetDuelRecordAsync(steady.UserId, duelsWon: 5, duelsLost: 5, points: 900);
        // Rookie: no duels at all => excluded from the duel board entirely.
        await SetDuelRecordAsync(rookie.UserId, duelsWon: 0, duelsLost: 0, points: 50);

        var response = await TestApi.GetAsync(sharp, "/api/players/duel-leaderboard");
        await TestApi.EnsureStatusAsync(response, HttpStatusCode.OK);
        var board = await TestApi.ReadAsAsync<List<DuelLeaderboardEntryResponseDto>>(response);

        var sharpEntry = board.Single(entry => entry.UserId == sharp.UserId);
        var steadyEntry = board.Single(entry => entry.UserId == steady.UserId);

        Assert.Equal(10, sharpEntry.DuelsPlayed);
        Assert.Equal(9, sharpEntry.DuelsWon);
        Assert.Equal(0.9m, sharpEntry.WinRate);
        Assert.Equal(0.5m, steadyEntry.WinRate);

        // Higher win rate ranks above more points; players without any duels never appear.
        Assert.True(board.IndexOf(sharpEntry) < board.IndexOf(steadyEntry));
        Assert.DoesNotContain(board, entry => entry.UserId == rookie.UserId);
    }

    [Fact]
    public async Task ActiveSessions_ReturnsOnlyActivePlayers()
    {
        var activePlayer = await RegisterAndLoginAsync("ActivePlayer");
        var inactivePlayer = await RegisterAndLoginAsync("InactivePlayer");

        var hallId = await CreateHallAsync(activePlayer.UserId, "Players Hall");

        await Factory.ExecuteDbContextAsync(async dbContext =>
        {
            dbContext.Sessions.Add(new Session
            {
                Id = Guid.NewGuid(),
                UserId = activePlayer.UserId,
                PoolHallId = hallId,
                IsActive = true,
                StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-20)
            });

            dbContext.Sessions.Add(new Session
            {
                Id = Guid.NewGuid(),
                UserId = inactivePlayer.UserId,
                PoolHallId = hallId,
                IsActive = false,
                StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-40),
                EndedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10)
            });

            await dbContext.SaveChangesAsync();
        });

        var response = await TestApi.GetAsync(activePlayer, "/api/players/active-sessions");
        await TestApi.EnsureStatusAsync(response, HttpStatusCode.OK);
        var players = await TestApi.ReadAsAsync<List<ActiveSessionPlayerResponseDto>>(response);

        Assert.Contains(players, player => player.UserId == activePlayer.UserId);
        Assert.DoesNotContain(players, player => player.UserId == inactivePlayer.UserId);
    }

    private async Task<Guid> CreateHallAsync(Guid addedByUserId, string name)
    {
        return await Factory.ExecuteDbContextAsync(async dbContext =>
        {
            var hall = new PoolHall
            {
                Id = Guid.NewGuid(),
                Name = name,
                Address = "Arena Street",
                Latitude = 44.81,
                Longitude = 20.42,
                TotalTables = 10,
                AddedByUserId = addedByUserId
            };

            dbContext.PoolHalls.Add(hall);
            await dbContext.SaveChangesAsync();
            return hall.Id;
        });
    }

    private async Task CreateReportedSessionAsync(
        Guid userId,
        Guid hallId,
        int gamesWon,
        int gamesLost,
        int ballsPotted)
    {
        await Factory.ExecuteDbContextAsync(async dbContext =>
        {
            var session = new Session
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PoolHallId = hallId,
                IsActive = false,
                StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-60),
                EndedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
                Report = new SessionReport
                {
                    SessionId = Guid.Empty,
                    GamesWon = gamesWon,
                    GamesLost = gamesLost,
                    BallsPotted = ballsPotted,
                    SnookersEscaped = 0,
                    FlaggedForValidation = false,
                    SubmittedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10)
                }
            };

            session.Report!.SessionId = session.Id;

            dbContext.Sessions.Add(session);
            await dbContext.SaveChangesAsync();
        });
    }

    private async Task SetPointsAsync(Guid userId, int points)
    {
        await Factory.ExecuteDbContextAsync(async dbContext =>
        {
            var profile = await dbContext.PlayerProfiles.SingleOrDefaultAsync(current => current.UserId == userId);

            if (profile is null)
            {
                profile = new PlayerProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = userId
                };

                dbContext.PlayerProfiles.Add(profile);
            }

            profile.Points = points;
            await dbContext.SaveChangesAsync();
        });
    }

    private async Task SetExperienceAsync(Guid userId, long experience)
    {
        await Factory.ExecuteDbContextAsync(async dbContext =>
        {
            var profile = await dbContext.PlayerProfiles.SingleOrDefaultAsync(current => current.UserId == userId);

            if (profile is null)
            {
                profile = new PlayerProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = userId
                };

                dbContext.PlayerProfiles.Add(profile);
            }

            profile.Experience = experience;
            await dbContext.SaveChangesAsync();
        });
    }

    private async Task SetDuelRecordAsync(Guid userId, int duelsWon, int duelsLost, int points)
    {
        await Factory.ExecuteDbContextAsync(async dbContext =>
        {
            var profile = await dbContext.PlayerProfiles.SingleOrDefaultAsync(current => current.UserId == userId);

            if (profile is null)
            {
                profile = new PlayerProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = userId
                };

                dbContext.PlayerProfiles.Add(profile);
            }

            profile.DuelsWon = duelsWon;
            profile.DuelsLost = duelsLost;
            profile.Points = points;
            await dbContext.SaveChangesAsync();
        });
    }
}
