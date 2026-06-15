using Microsoft.EntityFrameworkCore;
using PoolTracker.Api.Domain.Entities;

namespace PoolTracker.Api.Data;

public static class SeedDataExtensions
{
    public static async Task SeedAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PoolTrackerDbContext>();

        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        if (await dbContext.CueItems.AnyAsync(cancellationToken))
        {
            return;
        }

        var cueItems = new[]
        {
            new CueItem
            {
                Id = Guid.NewGuid(),
                Name = "Street Maple",
                ColorHex = "#b97745",
                Rarity = CueRarity.Common,
                ShopCost = 90,
                PowerBonus = 2,
                AccuracyBonus = 1,
                CueControlBonus = 1,
                SpinBonus = 0
            },
            new CueItem
            {
                Id = Guid.NewGuid(),
                Name = "Neon Break",
                ColorHex = "#5d6af5",
                Rarity = CueRarity.Rare,
                ShopCost = 180,
                PowerBonus = 2,
                AccuracyBonus = 3,
                CueControlBonus = 2,
                SpinBonus = 1
            },
            new CueItem
            {
                Id = Guid.NewGuid(),
                Name = "Velvet Rider",
                ColorHex = "#1d7a59",
                Rarity = CueRarity.Epic,
                ShopCost = 320,
                PowerBonus = 4,
                AccuracyBonus = 4,
                CueControlBonus = 3,
                SpinBonus = 2
            },
            new CueItem
            {
                Id = Guid.NewGuid(),
                Name = "Two Thousand Pots",
                ColorHex = "#f4b000",
                Rarity = CueRarity.Legendary,
                ShopCost = null,
                AchievementCode = "pot_2000_balls",
                PowerBonus = 5,
                AccuracyBonus = 5,
                CueControlBonus = 4,
                SpinBonus = 4
            }
        };

        dbContext.CueItems.AddRange(cueItems);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
