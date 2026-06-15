namespace PoolTracker.Api.Domain.Entities;

public enum CueRarity
{
    Common = 1,
    Rare = 2,
    Epic = 3,
    Legendary = 4
}

public sealed class CueItem
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string ColorHex { get; set; } = "#9ca3af";

    public CueRarity Rarity { get; set; }

    public int? ShopCost { get; set; }

    public string? AchievementCode { get; set; }

    public decimal PowerBonus { get; set; }

    public decimal AccuracyBonus { get; set; }

    public decimal CueControlBonus { get; set; }

    public decimal SpinBonus { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
