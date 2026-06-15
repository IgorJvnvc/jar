using System.Net;
using Microsoft.EntityFrameworkCore;
using PoolTracker.Api.Data;
using PoolTracker.Api.Domain.Entities;
using PoolTracker.Api.Tests.Infrastructure;

namespace PoolTracker.Api.Tests;

public sealed class HallsTests : IntegrationTestBase
{
    public HallsTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task AddHall_CreatesHallAndAwardsPoints()
    {
        var session = await RegisterAndLoginAsync("HallAdder");

        var add = await TestApi.PostAsync(session, "/api/halls", new
        {
            name = "Downtown Billiards",
            address = "Center 12",
            latitude = 44.81,
            longitude = 20.46,
            totalTables = 10
        });

        await TestApi.EnsureStatusAsync(add, HttpStatusCode.Created);
        var hall = await TestApi.ReadAsAsync<PoolHallResponseDto>(add);
        Assert.Equal("Downtown Billiards", hall.Name);

        var profile = await GetProfileAsync(session);
        Assert.Equal(15, profile.Points);
    }

    [Fact]
    public async Task GetHalls_ReturnsAddedHall()
    {
        var session = await RegisterAndLoginAsync("HallList");
        await AddHallThroughApiAsync(session, "List Hall", "Street 1");

        var response = await TestApi.GetAsync(session, "/api/halls");
        await TestApi.EnsureStatusAsync(response, HttpStatusCode.OK);

        var halls = await TestApi.ReadAsAsync<List<PoolHallResponseDto>>(response);
        Assert.Contains(halls, hall => hall.Name == "List Hall");
    }

    [Fact]
    public async Task GetHallDetail_ReturnsTablesAndScores()
    {
        var session = await RegisterAndLoginAsync("HallDetail");
        var hall = await AddHallThroughApiAsync(session, "Detail Hall", "Street 2");

        var addTable = await TestApi.PostAsync(session, $"/api/halls/{hall.Id}/tables", new
        {
            tableLabel = "Table 1"
        });
        await TestApi.EnsureStatusAsync(addTable, HttpStatusCode.Created);

        var detail = await TestApi.GetAsync(session, $"/api/halls/{hall.Id}");
        await TestApi.EnsureStatusAsync(detail, HttpStatusCode.OK);
        var payload = await TestApi.ReadAsAsync<PoolHallDetailResponseDto>(detail);

        Assert.Equal(hall.Id, payload.Id);
        Assert.Contains(payload.Tables, table => table.TableLabel == "Table 1");
    }

    [Fact]
    public async Task AddTable_WithDuplicateLabel_ReturnsConflict()
    {
        var session = await RegisterAndLoginAsync("HallTableDup");
        var hall = await AddHallThroughApiAsync(session, "Dup Hall", "Street 3");

        var first = await TestApi.PostAsync(session, $"/api/halls/{hall.Id}/tables", new
        {
            tableLabel = "Table A"
        });
        await TestApi.EnsureStatusAsync(first, HttpStatusCode.Created);

        var second = await TestApi.PostAsync(session, $"/api/halls/{hall.Id}/tables", new
        {
            tableLabel = "Table A"
        });

        await TestApi.EnsureStatusAsync(second, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RateHall_WithoutCompletedSession_ReturnsBadRequest()
    {
        var session = await RegisterAndLoginAsync("HallRateNoSession");
        var hall = await AddHallThroughApiAsync(session, "Rate Hall", "Street 4");

        var response = await TestApi.PostAsync(session, $"/api/halls/{hall.Id}/ratings", new
        {
            tableQuality = 7,
            ballsQuality = 8,
            cueQuality = 7,
            priceValue = 6,
            lighting = 8,
            comment = "good"
        });

        await TestApi.EnsureStatusAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RateHall_WithCompletedSession_ReturnsOkAndAwardsPoints()
    {
        var session = await RegisterAndLoginAsync("HallRateOk");
        var hall = await AddHallThroughApiAsync(session, "Rateable Hall", "Street 5");

        await CreateCompletedSessionAsync(session.UserId, hall.Id, null, DateTimeOffset.UtcNow.AddHours(-1));

        var response = await TestApi.PostAsync(session, $"/api/halls/{hall.Id}/ratings", new
        {
            tableQuality = 7,
            ballsQuality = 8,
            cueQuality = 7,
            priceValue = 6,
            lighting = 8,
            comment = "good"
        });

        await TestApi.EnsureStatusAsync(response, HttpStatusCode.OK);

        var profile = await GetProfileAsync(session);
        Assert.Equal(19, profile.Points);
    }

    [Fact]
    public async Task RateHall_TwiceSameDay_ReturnsConflict()
    {
        var session = await RegisterAndLoginAsync("HallRateDup");
        var hall = await AddHallThroughApiAsync(session, "Dup Rate Hall", "Street 6");
        await CreateCompletedSessionAsync(session.UserId, hall.Id, null, DateTimeOffset.UtcNow.AddHours(-2));

        var first = await TestApi.PostAsync(session, $"/api/halls/{hall.Id}/ratings", new
        {
            tableQuality = 7,
            ballsQuality = 8,
            cueQuality = 7,
            priceValue = 6,
            lighting = 8,
            comment = "first"
        });
        await TestApi.EnsureStatusAsync(first, HttpStatusCode.OK);

        var second = await TestApi.PostAsync(session, $"/api/halls/{hall.Id}/ratings", new
        {
            tableQuality = 7,
            ballsQuality = 8,
            cueQuality = 7,
            priceValue = 6,
            lighting = 8,
            comment = "second"
        });

        await TestApi.EnsureStatusAsync(second, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RateTable_WithCompletedTableSession_ReturnsOk()
    {
        var session = await RegisterAndLoginAsync("TableRateOk");
        var hall = await AddHallThroughApiAsync(session, "Table Hall", "Street 7");

        var tableId = await Factory.ExecuteDbContextAsync(async dbContext =>
        {
            var table = new PoolHallTable
            {
                Id = Guid.NewGuid(),
                PoolHallId = hall.Id,
                TableLabel = "Table Z",
                AddedByUserId = session.UserId
            };

            dbContext.PoolHallTables.Add(table);
            await dbContext.SaveChangesAsync();
            return table.Id;
        });

        await CreateCompletedSessionAsync(session.UserId, hall.Id, tableId, DateTimeOffset.UtcNow.AddHours(-1));

        var rate = await TestApi.PostAsync(session, $"/api/halls/tables/{tableId}/ratings", new
        {
            clothQuality = 8,
            cushionQuality = 8,
            levelness = 9,
            comment = "clean table"
        });

        await TestApi.EnsureStatusAsync(rate, HttpStatusCode.OK);
    }

    [Fact]
    public async Task RateTable_WithoutCompletedSession_ReturnsBadRequest()
    {
        var session = await RegisterAndLoginAsync("TableRateFail");
        var hall = await AddHallThroughApiAsync(session, "Table Hall 2", "Street 8");

        var tableId = await Factory.ExecuteDbContextAsync(async dbContext =>
        {
            var table = new PoolHallTable
            {
                Id = Guid.NewGuid(),
                PoolHallId = hall.Id,
                TableLabel = "Table Y",
                AddedByUserId = session.UserId
            };

            dbContext.PoolHallTables.Add(table);
            await dbContext.SaveChangesAsync();
            return table.Id;
        });

        var rate = await TestApi.PostAsync(session, $"/api/halls/tables/{tableId}/ratings", new
        {
            clothQuality = 8,
            cushionQuality = 8,
            levelness = 9,
            comment = "no session"
        });

        await TestApi.EnsureStatusAsync(rate, HttpStatusCode.BadRequest);
    }

    private async Task<PoolHallResponseDto> AddHallThroughApiAsync(TestAuthSession session, string name, string address)
    {
        var add = await TestApi.PostAsync(session, "/api/halls", new
        {
            name,
            address,
            latitude = 44.8,
            longitude = 20.4,
            totalTables = 6
        });

        await TestApi.EnsureStatusAsync(add, HttpStatusCode.Created);
        return await TestApi.ReadAsAsync<PoolHallResponseDto>(add);
    }

    private async Task CreateCompletedSessionAsync(
        Guid userId,
        Guid hallId,
        Guid? tableId,
        DateTimeOffset endedAtUtc)
    {
        await Factory.ExecuteDbContextAsync(async dbContext =>
        {
            var session = new Session
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PoolHallId = hallId,
                PoolHallTableId = tableId,
                StartedAtUtc = endedAtUtc.AddMinutes(-45),
                EndedAtUtc = endedAtUtc,
                IsActive = false,
                Report = new SessionReport
                {
                    SessionId = Guid.Empty,
                    BallsPotted = 20,
                    GamesWon = 2,
                    GamesLost = 1,
                    SnookersEscaped = 1,
                    FlaggedForValidation = false,
                    SubmittedAtUtc = endedAtUtc
                }
            };

            session.Report!.SessionId = session.Id;

            dbContext.Sessions.Add(session);
            await dbContext.SaveChangesAsync();
        });
    }

    private static async Task<ProfileResponseDto> GetProfileAsync(TestAuthSession session)
    {
        var response = await TestApi.GetAsync(session, "/api/profile/me");
        await TestApi.EnsureStatusAsync(response, HttpStatusCode.OK);
        return await TestApi.ReadAsAsync<ProfileResponseDto>(response);
    }
}
