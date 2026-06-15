namespace PoolTracker.Api.Domain.Entities;

public sealed class PoolHallRating
{
    public Guid Id { get; set; }

    public Guid PoolHallId { get; set; }

    public Guid UserId { get; set; }

    public int TableQuality { get; set; }

    public int BallsQuality { get; set; }

    public int CueQuality { get; set; }

    public int PriceValue { get; set; }

    public int Lighting { get; set; }

    public decimal OverallScore { get; set; }

    public string? Comment { get; set; }

    public DateOnly RatingDate { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public PoolHall PoolHall { get; set; } = null!;

    public ApplicationUser User { get; set; } = null!;
}
