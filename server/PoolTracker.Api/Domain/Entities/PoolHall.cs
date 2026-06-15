namespace PoolTracker.Api.Domain.Entities;

public sealed class PoolHall
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public int TotalTables { get; set; }

    public Guid AddedByUserId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ApplicationUser AddedByUser { get; set; } = null!;

    public ICollection<PoolHallTable> Tables { get; set; } = [];

    public ICollection<PoolHallRating> Ratings { get; set; } = [];
}
