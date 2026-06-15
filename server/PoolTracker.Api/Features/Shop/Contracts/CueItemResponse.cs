using PoolTracker.Api.Domain.Entities;

namespace PoolTracker.Api.Features.Shop.Contracts;

public sealed record CueItemResponse(
    Guid Id,
    string Name,
    string ColorHex,
    CueRarity Rarity,
    int? ShopCost,
    string? AchievementCode,
    decimal PowerBonus,
    decimal AccuracyBonus,
    decimal CueControlBonus,
    decimal SpinBonus,
    bool IsOwned,
    bool IsEquipped
);
