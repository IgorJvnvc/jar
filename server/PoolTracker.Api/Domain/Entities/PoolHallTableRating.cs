namespace PoolTracker.Api.Domain.Entities;

public sealed class PoolHallTableRating
{
    public Guid Id { get; set; }

    public Guid PoolHallTableId { get; set; }

    public Guid UserId { get; set; }

    public int ClothQuality { get; set; }

    public int CushionQuality { get; set; }

    public int Levelness { get; set; }

    public decimal OverallScore { get; set; }

    public string? Comment { get; set; }

    public DateOnly RatingDate { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public PoolHallTable PoolHallTable { get; set; } = null!;

    public ApplicationUser User { get; set; } = null!;
}
