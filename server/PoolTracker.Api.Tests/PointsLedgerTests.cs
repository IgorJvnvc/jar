using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PoolTracker.Api.Domain.Entities;
using PoolTracker.Api.Services;
using PoolTracker.Api.Tests.Infrastructure;

namespace PoolTracker.Api.Tests;

public sealed class PointsLedgerTests : IntegrationTestBase
{
    public PointsLedgerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task AwardPoints_IncreasesBalanceAndCreatesTransaction()
    {
        var user = await RegisterAndLoginAsync("LedgerAward");

        await Factory.ExecuteScopedAsync(async provider =>
        {
            var ledger = provider.GetRequiredService<IPointsLedgerService>();
            var dbContext = provider.GetRequiredService<PoolTracker.Api.Data.PoolTrackerDbContext>();

            await ledger.AwardPointsAsync(
                user.UserId,
                45,
                PointsTransactionType.ManualAdjustment,
                "bonus",
                null,
                CancellationToken.None);

            await dbContext.SaveChangesAsync();
        });

        var state = await Factory.ExecuteDbContextAsync(async dbContext =>
        {
            var profile = await dbContext.PlayerProfiles.SingleAsync(current => current.UserId == user.UserId);
            var transaction = await dbContext.PointsTransactions.SingleAsync(current => current.UserId == user.UserId);

            return (profile.Points, profile.DebtPoints, transaction.PointsDelta, transaction.Type);
        });

        Assert.Equal(45, state.Points);
        Assert.Equal(0, state.DebtPoints);
        Assert.Equal(45, state.PointsDelta);
        Assert.Equal(PointsTransactionType.ManualAdjustment, state.Type);
    }

    [Fact]
    public async Task DeductPoints_WithSufficientBalance_DoesNotCreateDebt()
    {
        var user = await RegisterAndLoginAsync("LedgerDeductEnough");
        await SeedProfileAsync(user.UserId, points: 120, debt: 0, title: null);

        await Factory.ExecuteScopedAsync(async provider =>
        {
            var ledger = provider.GetRequiredService<IPointsLedgerService>();
            var dbContext = provider.GetRequiredService<PoolTracker.Api.Data.PoolTrackerDbContext>();

            await ledger.DeductPointsAllowDebtAsync(
                user.UserId,
                75,
                PointsTransactionType.DuelLoss,
                "loss",
                null,
                CancellationToken.None);

            await dbContext.SaveChangesAsync();
        });

        var profile = await GetProfileStateAsync(user.UserId);
        Assert.Equal(45, profile.Points);
        Assert.Equal(0, profile.DebtPoints);
        Assert.Null(profile.Title);
    }

    [Fact]
    public async Task DeductPoints_WithInsufficientBalance_CreatesDebtAndTitle()
    {
        var user = await RegisterAndLoginAsync("LedgerDebt");
        await SeedProfileAsync(user.UserId, points: 30, debt: 0, title: null);

        await Factory.ExecuteScopedAsync(async provider =>
        {
            var ledger = provider.GetRequiredService<IPointsLedgerService>();
            var dbContext = provider.GetRequiredService<PoolTracker.Api.Data.PoolTrackerDbContext>();

            await ledger.DeductPointsAllowDebtAsync(
                user.UserId,
                100,
                PointsTransactionType.DuelLoss,
                "loss",
                null,
                CancellationToken.None);

            await dbContext.SaveChangesAsync();
        });

        var profile = await GetProfileStateAsync(user.UserId);
        Assert.Equal(0, profile.Points);
        Assert.Equal(70, profile.DebtPoints);
        Assert.Equal(PlayerProfile.DebtTitle, profile.Title);
    }

    [Fact]
    public async Task PayDebt_PartialPayment_ReducesDebtAndPoints()
    {
        var user = await RegisterAndLoginAsync("LedgerPayPartial");
        await SeedProfileAsync(user.UserId, points: 200, debt: 150, title: PlayerProfile.DebtTitle);

        var paid = await Factory.ExecuteScopedAsync(async provider =>
        {
            var ledger = provider.GetRequiredService<IPointsLedgerService>();
            var dbContext = provider.GetRequiredService<PoolTracker.Api.Data.PoolTrackerDbContext>();

            var result = await ledger.PayDebtAsync(user.UserId, 80, CancellationToken.None);
            await dbContext.SaveChangesAsync();
            return result;
        });

        Assert.Equal(80, paid);

        var profile = await GetProfileStateAsync(user.UserId);
        Assert.Equal(120, profile.Points);
        Assert.Equal(70, profile.DebtPoints);
        Assert.Equal(PlayerProfile.DebtTitle, profile.Title);
    }

    [Fact]
    public async Task PayDebt_FullPayment_ClearsDebtAndTitle()
    {
        var user = await RegisterAndLoginAsync("LedgerPayFull");
        await SeedProfileAsync(user.UserId, points: 200, debt: 80, title: PlayerProfile.DebtTitle);

        var paid = await Factory.ExecuteScopedAsync(async provider =>
        {
            var ledger = provider.GetRequiredService<IPointsLedgerService>();
            var dbContext = provider.GetRequiredService<PoolTracker.Api.Data.PoolTrackerDbContext>();

            var result = await ledger.PayDebtAsync(user.UserId, 300, CancellationToken.None);
            await dbContext.SaveChangesAsync();
            return result;
        });

        Assert.Equal(80, paid);

        var profile = await GetProfileStateAsync(user.UserId);
        Assert.Equal(120, profile.Points);
        Assert.Equal(0, profile.DebtPoints);
        Assert.Null(profile.Title);
    }

    [Fact]
    public async Task GetOrCreateProfile_CreatesMissingProfile()
    {
        var user = await RegisterAndLoginAsync("LedgerCreateProfile");

        var profileId = await Factory.ExecuteScopedAsync(async provider =>
        {
            var ledger = provider.GetRequiredService<IPointsLedgerService>();
            var dbContext = provider.GetRequiredService<PoolTracker.Api.Data.PoolTrackerDbContext>();

            var profile = await ledger.GetOrCreateProfileAsync(user.UserId, CancellationToken.None);
            await dbContext.SaveChangesAsync();
            return profile.Id;
        });

        Assert.NotEqual(Guid.Empty, profileId);

        var exists = await Factory.ExecuteDbContextAsync(async dbContext =>
        {
            return await dbContext.PlayerProfiles.AnyAsync(current => current.UserId == user.UserId);
        });

        Assert.True(exists);
    }

    private async Task SeedProfileAsync(Guid userId, int points, int debt, string? title)
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
            profile.DebtPoints = debt;
            profile.Title = title;

            await dbContext.SaveChangesAsync();
        });
    }

    private async Task<(int Points, int DebtPoints, string? Title)> GetProfileStateAsync(Guid userId)
    {
        return await Factory.ExecuteDbContextAsync(async dbContext =>
        {
            var profile = await dbContext.PlayerProfiles.SingleAsync(current => current.UserId == userId);
            return (profile.Points, profile.DebtPoints, profile.Title);
        });
    }
}
