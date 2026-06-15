namespace PoolTracker.Api.Domain.Entities;

public sealed class UserCueInventory
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid CueItemId { get; set; }

    public DateTimeOffset AcquiredAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public bool IsEquipped { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public CueItem CueItem { get; set; } = null!;
}
