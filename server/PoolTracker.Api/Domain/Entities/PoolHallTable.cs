namespace PoolTracker.Api.Domain.Entities;

public sealed class PoolHallTable
{
    public Guid Id { get; set; }

    public Guid PoolHallId { get; set; }

    public string TableLabel { get; set; } = string.Empty;

    public Guid AddedByUserId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public PoolHall PoolHall { get; set; } = null!;

    public ApplicationUser AddedByUser { get; set; } = null!;

    public ICollection<PoolHallTableRating> Ratings { get; set; } = [];
}
