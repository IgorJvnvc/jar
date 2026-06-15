using System.ComponentModel.DataAnnotations;

namespace PoolTracker.Api.Features.Duels.Contracts;

public sealed class RespondDuelRequest
{
    [Required]
    public bool Accept { get; init; }
}
