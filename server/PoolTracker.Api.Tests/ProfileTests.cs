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

        // A brand-new player starts at level 1 with no experience.
        Assert.Equal(1, profile.Level);
        Assert.Equal("Greenhorn", profile.LevelTitle);
        Assert.Equal(0, profile.Experience);
        Assert.Equal(0, profile.ExperienceIntoLevel);
        Assert.Equal(80, profile.ExperienceForNextLevel);
    }

    [Fact]
    public async Task GetProfile_DerivesLevelAndTitleFromExperience()
    {
        var session = await RegisterAndLoginAsync("ProfileLevel");

        await Factory.ExecuteDbContextAsync(async dbContext =>
        {
            dbContext.PlayerProfiles.Add(new PlayerProfile
            {
                Id = Guid.NewGuid(),
                UserId = session.UserId,
                Experience = 460, // exactly the start of level 5
                AvatarColorHex = "#1d7a59",
                Power = 50,
                Accuracy = 50,
                CueControl = 50,
                Spin = 50
            });

            await dbContext.SaveChangesAsync();
        });

        var response = await TestApi.GetAsync(session, "/api/profile/me");
        await TestApi.EnsureStatusAsync(response, HttpStatusCode.OK);
        var profile = await TestApi.ReadAsAsync<ProfileResponseDto>(response);

        Assert.Equal(5, profile.Level);
        Assert.Equal("Drifter", profile.LevelTitle);
        Assert.Equal(460, profile.Experience);
        Assert.Equal(0, profile.ExperienceIntoLevel);
        Assert.Equal(195, profile.ExperienceForNextLevel);
        // No debt, so the title slot falls through to the level title on the client.
        Assert.Null(profile.Title);
    }

    [Fact]
    public async Task GetProfile_IncludesDuelAndGeneralRecords()
    {
        var session = await RegisterAndLoginAsync("ProfileRecords");

        await Factory.ExecuteDbContextAsync(async dbContext =>
        {
            var hall = new PoolHall
            {
                Id = Guid.NewGuid(),
                Name = "Records Hall",
                Address = "Records Street",
                Latitude = 44.81,
                Longitude = 20.42,
                TotalTables = 6,
                AddedByUserId = session.UserId
            };

            dbContext.PoolHalls.Add(hall);

            dbContext.PlayerProfiles.Add(new PlayerProfile
            {
                Id = Guid.NewGuid(),
                UserId = session.UserId,
                DuelsWon = 4,
                DuelsLost = 1,
                AvatarColorHex = "#1d7a59",
                Power = 50,
                Accuracy = 50,
                CueControl = 50,
                Spin = 50
            });

            var played = new Session
            {
                Id = Guid.NewGuid(),
                UserId = session.UserId,
                PoolHallId = hall.Id,
                IsActive = false,
                StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-60),
                EndedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
                Report = new SessionReport
                {
                    SessionId = Guid.Empty,
                    GamesWon = 7,
                    GamesLost = 3,
                    BallsPotted = 20,
                    SnookersEscaped = 0,
                    FlaggedForValidation = false,
                    SubmittedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10)
                }
            };

            played.Report!.SessionId = played.Id;
            dbContext.Sessions.Add(played);

            await dbContext.SaveChangesAsync();
        });

        var response = await TestApi.GetAsync(session, "/api/profile/me");
        await TestApi.EnsureStatusAsync(response, HttpStatusCode.OK);
        var profile = await TestApi.ReadAsAsync<ProfileResponseDto>(response);

        Assert.Equal(4, profile.DuelsWon);
        Assert.Equal(1, profile.DuelsLost);
        Assert.Equal(7, profile.GamesWon);
        Assert.Equal(3, profile.GamesLost);
    }

    [Fact]
    public async Task UpdateProfile_PersistsDisplayFields_AndIgnoresRetiredStats()
    {
        var session = await RegisterAndLoginAsync("ProfileUpdate");

        var update = await TestApi.PutAsync(session, "/api/profile/me", new
        {
            displayName = "Updated Name",
            avatarColorHex = "#445566",
            favoriteBallNumber = 8,
            // Retired self-rated attributes: the server must ignore these even if a stale client sends them.
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

        // Attributes are no longer user-editable; they remain at their seeded default (50).
        Assert.Equal(50m, profile.Power);
        Assert.Equal(50m, profile.Accuracy);
        Assert.Equal(50m, profile.CueControl);
        Assert.Equal(50m, profile.Spin);
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
