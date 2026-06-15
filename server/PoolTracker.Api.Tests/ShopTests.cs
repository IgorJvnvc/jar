using System.Net;
using Microsoft.EntityFrameworkCore;
using PoolTracker.Api.Domain.Entities;
using PoolTracker.Api.Tests.Infrastructure;

namespace PoolTracker.Api.Tests;

public sealed class ShopTests : IntegrationTestBase
{
    public ShopTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task ListCues_ReturnsSeededCatalog()
    {
        var session = await RegisterAndLoginAsync("ShopCatalog");

        var response = await TestApi.GetAsync(session, "/api/shop/cues");
        await TestApi.EnsureStatusAsync(response, HttpStatusCode.OK);

        var cues = await TestApi.ReadAsAsync<List<CueItemResponseDto>>(response);
        Assert.True(cues.Count >= 4);
        Assert.Contains(cues, cue => cue.Name == "Street Maple");
    }

    [Fact]
    public async Task PurchaseCue_WithEnoughPoints_SucceedsAndDeductsPoints()
    {
        var session = await RegisterAndLoginAsync("ShopBuy");
        await SetProfilePointsAsync(session.UserId, 400);

        var cue = await GetPurchasableCueAsync(session);
        var purchase = await TestApi.PostAsync(session, "/api/shop/cues/purchase", new
        {
            cueItemId = cue.Id
        });

        await TestApi.EnsureStatusAsync(purchase, HttpStatusCode.OK);

        var profile = await GetProfileAsync(session);
        Assert.Equal(400 - cue.ShopCost!.Value, profile.Points);

        var list = await TestApi.GetAsync(session, "/api/shop/cues");
        await TestApi.EnsureStatusAsync(list, HttpStatusCode.OK);
        var cues = await TestApi.ReadAsAsync<List<CueItemResponseDto>>(list);
        var purchasedCue = cues.Single(current => current.Id == cue.Id);
        Assert.True(purchasedCue.IsOwned);
    }

    [Fact]
    public async Task PurchaseCue_WithInsufficientPoints_ReturnsBadRequest()
    {
        var session = await RegisterAndLoginAsync("ShopInsufficient");
        await SetProfilePointsAsync(session.UserId, 10);

        var cue = await GetPurchasableCueAsync(session);
        var purchase = await TestApi.PostAsync(session, "/api/shop/cues/purchase", new
        {
            cueItemId = cue.Id
        });

        await TestApi.EnsureStatusAsync(purchase, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PurchaseCue_Twice_ReturnsConflict()
    {
        var session = await RegisterAndLoginAsync("ShopDuplicate");
        await SetProfilePointsAsync(session.UserId, 400);

        var cue = await GetPurchasableCueAsync(session);

        var first = await TestApi.PostAsync(session, "/api/shop/cues/purchase", new
        {
            cueItemId = cue.Id
        });
        await TestApi.EnsureStatusAsync(first, HttpStatusCode.OK);

        var second = await TestApi.PostAsync(session, "/api/shop/cues/purchase", new
        {
            cueItemId = cue.Id
        });
        await TestApi.EnsureStatusAsync(second, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PurchaseAchievementOnlyCue_ReturnsBadRequest()
    {
        var session = await RegisterAndLoginAsync("ShopAchievement");
        await SetProfilePointsAsync(session.UserId, 1000);

        var legendaryCueId = await Factory.ExecuteDbContextAsync(async dbContext =>
        {
            return await dbContext.CueItems
                .Where(current => current.ShopCost == null)
                .Select(current => current.Id)
                .FirstAsync();
        });

        var purchase = await TestApi.PostAsync(session, "/api/shop/cues/purchase", new
        {
            cueItemId = legendaryCueId
        });

        await TestApi.EnsureStatusAsync(purchase, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task EquipCue_SetsEquippedFlag()
    {
        var session = await RegisterAndLoginAsync("ShopEquip");
        await SetProfilePointsAsync(session.UserId, 500);
        var cue = await GetPurchasableCueAsync(session);

        var purchase = await TestApi.PostAsync(session, "/api/shop/cues/purchase", new
        {
            cueItemId = cue.Id
        });
        await TestApi.EnsureStatusAsync(purchase, HttpStatusCode.OK);

        var equip = await TestApi.PostAsync(session, "/api/shop/cues/equip", new
        {
            cueItemId = cue.Id
        });
        await TestApi.EnsureStatusAsync(equip, HttpStatusCode.OK);

        var list = await TestApi.GetAsync(session, "/api/shop/cues");
        await TestApi.EnsureStatusAsync(list, HttpStatusCode.OK);
        var cues = await TestApi.ReadAsAsync<List<CueItemResponseDto>>(list);
        var equipped = cues.Single(current => current.Id == cue.Id);
        Assert.True(equipped.IsEquipped);
    }

    [Fact]
    public async Task EquipCue_WhenNotOwned_ReturnsNotFound()
    {
        var session = await RegisterAndLoginAsync("ShopEquipMissing");
        var cue = await GetPurchasableCueAsync(session);

        var equip = await TestApi.PostAsync(session, "/api/shop/cues/equip", new
        {
            cueItemId = cue.Id
        });

        await TestApi.EnsureStatusAsync(equip, HttpStatusCode.NotFound);
    }

    private async Task<CueItemResponseDto> GetPurchasableCueAsync(TestAuthSession session)
    {
        var response = await TestApi.GetAsync(session, "/api/shop/cues");
        await TestApi.EnsureStatusAsync(response, HttpStatusCode.OK);
        var cues = await TestApi.ReadAsAsync<List<CueItemResponseDto>>(response);
        return cues.First(current => current.ShopCost.HasValue);
    }

    private async Task SetProfilePointsAsync(Guid userId, int points)
    {
        await Factory.ExecuteDbContextAsync(async dbContext =>
        {
            var profile = await dbContext.PlayerProfiles.SingleOrDefaultAsync(current => current.UserId == userId);
            if (profile is null)
            {
                profile = new PlayerProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    AvatarColorHex = "#1d7a59",
                    Power = 50,
                    Accuracy = 50,
                    CueControl = 50,
                    Spin = 50
                };
                dbContext.PlayerProfiles.Add(profile);
            }

            profile.Points = points;
            profile.DebtPoints = 0;
            profile.Title = null;

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
