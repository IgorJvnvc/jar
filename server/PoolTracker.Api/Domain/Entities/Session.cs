namespace PoolTracker.Api.Domain.Entities;

public sealed class Session
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid PoolHallId { get; set; }

    public Guid? PoolHallTableId { get; set; }

    public DateTimeOffset StartedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? EndedAtUtc { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>How the session ended. Null while the session is still active.</summary>
    public SessionEndReason? EndReason { get; set; }

    public SessionReport? Report { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public PoolHall PoolHall { get; set; } = null!;

    public PoolHallTable? PoolHallTable { get; set; }
}
