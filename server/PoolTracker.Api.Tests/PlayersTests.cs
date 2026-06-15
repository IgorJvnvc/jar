using System.Net;
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
}
