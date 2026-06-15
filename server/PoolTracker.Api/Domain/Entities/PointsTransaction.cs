namespace PoolTracker.Api.Domain.Entities;

public enum PointsTransactionType
{
    SessionCompletion = 1,
    HallAdded = 2,
    HallRated = 3,
    TableRated = 4,
    DuelWin = 5,
    DuelLoss = 6,
    ShopPurchase = 7,
    ManualAdjustment = 8,
    DebtPayment = 9
}

public sealed class PointsTransaction
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public int PointsDelta { get; set; }

    public PointsTransactionType Type { get; set; }

    public string Description { get; set; } = string.Empty;

    public Guid? RelatedEntityId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ApplicationUser User { get; set; } = null!;
}
