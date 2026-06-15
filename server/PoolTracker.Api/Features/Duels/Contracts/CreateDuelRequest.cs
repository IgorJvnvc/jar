using System.ComponentModel.DataAnnotations;

namespace PoolTracker.Api.Features.Duels.Contracts;

public sealed class CreateDuelRequest
{
    [Required]
    public Guid OpponentUserId { get; init; }
}
