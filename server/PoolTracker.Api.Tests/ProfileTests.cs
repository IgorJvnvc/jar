using System.Net;
using PoolTracker.Api.Domain.Entities;
using PoolTracker.Api.Tests.Infrastructure;

namespace PoolTracker.Api.Tests;

public sealed class ProfileTests : IntegrationTestBase
{
    public ProfileTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task GetProfile_CreatesDefaultProfile_WhenMissing()
    {
        var session = await RegisterAndLoginAsync("ProfileAutoCreate");

        var response = await TestApi.GetAsync(session, "/api/profile/me");
        await TestApi.EnsureStatusAsync(response, HttpStatusCode.OK);

        var profile = await TestApi.ReadAsAsync<ProfileResponseDto>(response);
        Assert.Equal(session.UserId, profile.UserId);
        Assert.Equal(0, profile.Points);
        Assert.Equal(0, profile.DebtPoints);
        Assert.Equal("#1d7a59", profile.AvatarColorHex);
    }

    [Fact]
    public async Task UpdateProfile_PersistsUserAndStats()
    {
        var session = await RegisterAndLoginAsync("ProfileUpdate");

        var update = await TestApi.PutAsync(session, "/api/profile/me", new
        {
            displayName = "Updated Name",
            avatarColorHex = "#445566",
            favoriteBallNumber = 8,
            power = 71.5m,
            accuracy = 62.5m,
            cueControl = 59m,
            spin = 88.5m
        });

        await TestApi.EnsureStatusAsync(update, HttpStatusCode.OK);
        var profile = await TestApi.ReadAsAsync<ProfileResponseDto>(update);

        Assert.Equal("Updated Name", profile.DisplayName);
        Assert.Equal("#445566", profile.AvatarColorHex);
        Assert.Equal(8, profile.FavoriteBallNumber);
        Assert.Equal(71.5m, profile.Power);
        Assert.Equal(62.5m, profile.Accuracy);
        Assert.Equal(59m, profile.CueControl);
        Assert.Equal(88.5m, profile.Spin);
    }

    [Fact]
    public async Task PayDebt_WithNoDebt_ReturnsZeroPaid()
    {
        var session = await RegisterAndLoginAsync("NoDebt");

        var response = await TestApi.PostAsync(session, "/api/profile/pay-debt", new
        {
            amount = 100
        });

        await TestApi.EnsureStatusAsync(response, HttpStatusCode.OK);
        var payload = await TestApi.ReadAsAsync<PayDebtResponseDto>(response);

        Assert.Equal(0, payload.PaidPoints);
        Assert.Equal(0, payload.Profile.DebtPoints);
    }

    [Fact]
    public async Task PayDebt_WithOutstandingDebt_ReducesDebtAndPoints()
    {
        var session = await RegisterAndLoginAsync("DebtPay");

        await Factory.ExecuteDbContextAsync(async dbContext =>
        {
            dbContext.PlayerProfiles.Add(new PlayerProfile
            {
                Id = Guid.NewGuid(),
                UserId = session.UserId,
                Points = 170,
                DebtPoints = 120,
                Title = PlayerProfile.DebtTitle,
                AvatarColorHex = "#1d7a59",
                Power = 50,
                Accuracy = 50,
                CueControl = 50,
                Spin = 50
            });

            await dbContext.SaveChangesAsync();
        });

        var response = await TestApi.PostAsync(session, "/api/profile/pay-debt", new
        {
            amount = 90
        });

        await TestApi.EnsureStatusAsync(response, HttpStatusCode.OK);
        var payload = await TestApi.ReadAsAsync<PayDebtResponseDto>(response);

        Assert.Equal(90, payload.PaidPoints);
        Assert.Equal(80, payload.Profile.Points);
        Assert.Equal(30, payload.Profile.DebtPoints);
        Assert.Equal(PlayerProfile.DebtTitle, payload.Profile.Title);
    }

    [Fact]
    public async Task PayDebt_ClearsDebtTitle_WhenDebtIsFullyPaid()
    {
        var session = await RegisterAndLoginAsync("DebtClear");

        await Factory.ExecuteDbContextAsync(async dbContext =>
        {
            dbContext.PlayerProfiles.Add(new PlayerProfile
            {
                Id = Guid.NewGuid(),
                UserId = session.UserId,
                Points = 300,
                DebtPoints = 100,
                Title = PlayerProfile.DebtTitle,
                AvatarColorHex = "#1d7a59",
                Power = 50,
                Accuracy = 50,
                CueControl = 50,
                Spin = 50
            });

            await dbContext.SaveChangesAsync();
        });

        var response = await TestApi.PostAsync(session, "/api/profile/pay-debt", new
        {
            amount = 150
        });

        await TestApi.EnsureStatusAsync(response, HttpStatusCode.OK);
        var payload = await TestApi.ReadAsAsync<PayDebtResponseDto>(response);

        Assert.Equal(100, payload.PaidPoints);
        Assert.Equal(200, payload.Profile.Points);
        Assert.Equal(0, payload.Profile.DebtPoints);
        Assert.Null(payload.Profile.Title);
    }
}
