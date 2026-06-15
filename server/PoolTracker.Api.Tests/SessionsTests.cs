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
            ballsPotted = 38,
            gamesWon = 4,
            gamesLost = 2,
            snookersEscaped = 3,
            notes = "Great run"
        });

        await TestApi.EnsureStatusAsync(end, HttpStatusCode.OK);
        var payload = await TestApi.ReadAsAsync<SessionResponseDto>(end);

        Assert.False(payload.IsActive);
        Assert.False(payload.IsFlaggedForValidation);
        Assert.True(payload.AwardedPoints >= 20);

        var profile = await GetProfileAsync(session);
        Assert.Equal(payload.AwardedPoints, profile.Points);
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
            ballsPotted = 150,
            gamesWon = 1,
            gamesLost = 0,
            snookersEscaped = 0,
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
                ballsPotted = 10 + i,
                gamesWon = 1,
                gamesLost = 0,
                snookersEscaped = 0,
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
