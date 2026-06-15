namespace PoolTracker.Api.Domain.Entities;

public sealed class DuelResultSubmission
{
    public Guid Id { get; set; }

    public Guid DuelId { get; set; }

    public Guid SubmittedByUserId { get; set; }

    public int RoundNumber { get; set; } = 1;

    public DuelPlayerResultChoice Choice { get; set; }

    public DateTimeOffset SubmittedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public Duel Duel { get; set; } = null!;

    public ApplicationUser SubmittedByUser { get; set; } = null!;
}
